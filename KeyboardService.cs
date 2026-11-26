using System.Runtime.InteropServices;

namespace LangFix;

internal sealed class KeyboardService : IDisposable
{
    internal static bool AcceptInjectedForTesting { get; set; }
    internal static string LastTestDecision { get; private set; } = "";
    internal readonly record struct CorrectionPair(string Original, string Converted);
    private readonly record struct UndoCorrection(string Original, string Converted, char? Delimiter, nint TargetWindow);

    private readonly NativeMethods.HookProc callback;
    private readonly SynchronizationContext uiContext;
    private readonly object gate = new();
    private readonly List<char> englishToken = [];
    private readonly List<char> thaiToken = [];
    private readonly System.Windows.Forms.Timer hookHealthTimer;
    private NativeMethods.KeyboardLanguage sourceLanguage = NativeMethods.KeyboardLanguage.Unknown;
    private UndoCorrection? pendingUndo;
    private nint hook;
    private nint lastFocusedWindow;
    private bool autoCorrectionSuppressedForWindow;
    private bool capsLockOn;
    private uint lastHookTick;
    private long lastHookReinstall;
    private NativeMethods.KeyboardLanguage expectedLanguage = NativeMethods.KeyboardLanguage.Unknown;
    private long expectedLanguageUntil;
    private bool disposed;

    // Windows silently removes a low-level hook that is too slow too often. If the session keeps
    // receiving input while our callback has not run for this long, the hook is reinstalled.
    private const uint HookSilenceThresholdMs = 2000;
    private const int HookHealthIntervalMs = 3000;
    private const int MinReinstallIntervalMs = 10_000;
    // After asking a window to switch layout, trust the requested language for this long so the
    // first keys of the next word are not attributed to the old layout while the switch is applied.
    private const int ExpectedLanguageWindowMs = 600;

    // Skip automatic correction in terminals and password managers so commands and passwords
    // are never altered unexpectedly; manual shortcut still works everywhere.
    private static readonly HashSet<string> AutoExcludedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd", "powershell", "pwsh", "windowsterminal", "wt", "conhost",
        "keepass", "keepassxc", "1password", "bitwarden"
    };

    internal static bool IsAutoExcludedProcess(string processName) =>
        !string.IsNullOrEmpty(processName) && AutoExcludedProcesses.Contains(processName);

    public bool Enabled { get; set; } = true;
    public bool AutomaticMode { get; set; }
    public bool SwitchLayoutAfterCorrection { get; set; } = true;
    public ShortcutDefinition Shortcut { get; set; } = ShortcutDefinition.Default;
    public event Action<string, string>? CorrectionMade;
    public event Action<Exception>? CorrectionFailed;
    internal int HookReinstallCount { get; private set; }

    public KeyboardService()
    {
        uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        callback = HookCallback;
        hook = NativeMethods.InstallHook(callback);
        lastHookTick = unchecked((uint)Environment.TickCount);
        capsLockOn = NativeMethods.ReadCapsLockState();
        hookHealthTimer = new System.Windows.Forms.Timer { Interval = HookHealthIntervalMs };
        hookHealthTimer.Tick += (_, _) => CheckHookHealth();
        hookHealthTimer.Start();
    }

    private nint HookCallback(int code, nint wParam, nint lParam)
    {
        lastHookTick = unchecked((uint)Environment.TickCount);
        try
        {
            return HookCallbackCore(code, wParam, lParam);
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex);
            return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
        }
    }

    private void CheckHookHealth()
    {
        if (disposed) return;
        var lastInput = NativeMethods.GetLastInputTick();
        var silence = unchecked(lastInput - lastHookTick);
        // Wrap-around safe: a "negative" difference means our callback ran after the last input.
        if (silence <= HookSilenceThresholdMs || silence >= 0x8000_0000) return;
        if (Environment.TickCount64 - lastHookReinstall < MinReinstallIntervalMs) return;
        ReinstallHook();
    }

    internal void ReinstallHook()
    {
        try
        {
            // Install the new hook before removing the old one so there is no gap. Both calls run on
            // this thread, so the callback cannot be invoked between them (no double processing).
            var newHook = NativeMethods.InstallHook(callback);
            var oldHook = hook;
            hook = newHook;
            if (oldHook != 0) NativeMethods.UnhookWindowsHookEx(oldHook);
            lastHookReinstall = Environment.TickCount64;
            lastHookTick = unchecked((uint)Environment.TickCount);
            HookReinstallCount++;
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex);
        }
    }

    private nint HookCallbackCore(int code, nint wParam, nint lParam)
    {
        if (code < 0 || !Enabled) return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
        var data = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
        if ((data.flags & NativeMethods.LlkhfInjected) != 0 &&
            (data.dwExtraInfo == NativeMethods.LangFixInjectionMarker || !AcceptInjectedForTesting))
            return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);

        var message = (int)wParam;
        if (message is not (NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown))
            return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);

        var focusedWindow = NativeMethods.GetFocusedInputWindow();
        if (focusedWindow != lastFocusedWindow)
        {
            lock (gate)
            {
                ClearBuffers();
                pendingUndo = null;
                expectedLanguage = NativeMethods.KeyboardLanguage.Unknown;
            }
            lastFocusedWindow = focusedWindow;
            capsLockOn = NativeMethods.ReadCapsLockState();
            autoCorrectionSuppressedForWindow = IsAutoExcludedProcess(NativeMethods.GetProcessNameForWindow(focusedWindow));
        }

        if (data.vkCode == NativeMethods.VkCapital)
        {
            capsLockOn = !capsLockOn;
            return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
        }

        var controlDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VkControl) & 0x8000) != 0;
        var altDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VkMenu) & 0x8000) != 0;
        var shiftDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VkShift) & 0x8000) != 0;

        if (Shortcut.Matches(data.vkCode, controlDown, altDown, shiftDown))
        {
            UndoCorrection? undo;
            CorrectionPair? pair = null;
            lock (gate)
            {
                undo = pendingUndo is { } available && available.TargetWindow == focusedWindow ? available : null;
                pendingUndo = null;
                if (undo is null)
                    pair = ChooseManual(new string(englishToken.ToArray()), new string(thaiToken.ToArray()), sourceLanguage);
                ClearBuffers();
            }

            if (undo is { } correctionToUndo)
            {
                _ = CorrectAfterShortcutAsync(
                    correctionToUndo.Converted,
                    correctionToUndo.Original,
                    correctionToUndo.Delimiter,
                    correctionToUndo.Converted.Length + (correctionToUndo.Delimiter is null ? 0 : 1),
                    allowUndo: false);
                return 1;
            }
            if (pair is { } correction)
            {
                _ = CorrectAfterShortcutAsync(correction.Original, correction.Converted, null, null, allowUndo: true);
                return 1;
            }
            return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
        }

        if (!ShortcutDefinition.IsModifierKey((Keys)data.vkCode))
        {
            lock (gate) pendingUndo = null;
        }

        if (controlDown || altDown)
        {
            lock (gate) ClearBuffers();
            return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
        }

        if (data.vkCode == NativeMethods.VkBack)
        {
            lock (gate)
            {
                if (englishToken.Count > 0) englishToken.RemoveAt(englishToken.Count - 1);
                if (thaiToken.Count > 0) thaiToken.RemoveAt(thaiToken.Count - 1);
                if (englishToken.Count == 0 && thaiToken.Count == 0) sourceLanguage = NativeMethods.KeyboardLanguage.Unknown;
            }
            return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
        }

        if (IsAutomaticBoundaryKey(data.vkCode))
        {
            CorrectionPair? pair;
            lock (gate)
            {
                if (AcceptInjectedForTesting)
                    LastTestDecision = $"source={sourceLanguage}; en=[{new string(englishToken.ToArray())}] known={LanguageConverter.IsKnownEnglish(new string(englishToken.ToArray()))}; th=[{new string(thaiToken.ToArray())}] known={LanguageConverter.IsKnownThai(new string(thaiToken.ToArray()))}";
                pair = AutomaticMode && !autoCorrectionSuppressedForWindow
                    ? ChooseAutomatic(new string(englishToken.ToArray()), new string(thaiToken.ToArray()), sourceLanguage)
                    : null;
                ClearBuffers();
            }
            if (pair is { } correction)
            {
                // Correct synchronously inside the hook: the space key is suppressed and the
                // replacement is injected before any later physical key is processed.
                Correct(correction.Original, correction.Converted, ' ');
                return 1;
            }
            return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
        }

        var state = NativeMethods.GetActiveKeyboardState(capsLockOn);
        var english = NativeMethods.TranslateKey(data.vkCode, data.scanCode, state, NativeMethods.KeyboardLanguage.English);
        var thai = NativeMethods.TranslateKey(data.vkCode, data.scanCode, state, NativeMethods.KeyboardLanguage.Thai);
        if (english.Length == 1 && thai.Length == 1 && !char.IsControl(english[0]) && !char.IsControl(thai[0]))
        {
            var activeLanguage = ResolveActiveLanguage();
            CorrectionPair? immediateCorrection = null;
            lock (gate)
            {
                if (englishToken.Count == 0 && thaiToken.Count == 0)
                    sourceLanguage = activeLanguage;
                else if (activeLanguage != NativeMethods.KeyboardLanguage.Unknown && sourceLanguage != activeLanguage)
                {
                    ClearBuffers();
                    sourceLanguage = activeLanguage;
                }

                if (englishToken.Count >= 80) ClearBuffers();
                englishToken.Add(english[0]);
                thaiToken.Add(thai[0]);

                if (AutomaticMode && !autoCorrectionSuppressedForWindow)
                {
                    immediateCorrection = ChooseImmediateAutomatic(
                        new string(englishToken.ToArray()),
                        new string(thaiToken.ToArray()),
                        sourceLanguage);
                    if (immediateCorrection is not null) ClearBuffers();
                }
            }

            if (immediateCorrection is { } correction)
            {
                // The current physical key has not reached the target control yet. Suppress it,
                // erase only the preceding characters, then insert the complete corrected word.
                // This runs synchronously so a fast next key cannot land before the backspaces.
                Correct(correction.Original, correction.Converted, null, correction.Original.Length - 1);
                return 1;
            }
        }
        else if (data.vkCode is 0x0D or 0x09 or 0x25 or 0x26 or 0x27 or 0x28 or 0x2E)
        {
            lock (gate) ClearBuffers();
        }

        return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
    }

    internal static CorrectionPair? ChooseManual(string english, string thai, NativeMethods.KeyboardLanguage source)
    {
        if (english.Length == 0 || thai.Length == 0) return null;
        return source switch
        {
            NativeMethods.KeyboardLanguage.Thai => new CorrectionPair(thai, english),
            NativeMethods.KeyboardLanguage.English => new CorrectionPair(english, thai),
            _ when LanguageConverter.IsKnownEnglish(english) && !LanguageConverter.IsKnownThai(thai) => new CorrectionPair(thai, english),
            _ => new CorrectionPair(english, thai)
        };
    }

    internal static CorrectionPair? ChooseAutomatic(string english, string thai, NativeMethods.KeyboardLanguage source)
    {
        if (english.Length < 3 || thai.Length < 3) return null;
        var englishKnown = LanguageConverter.IsKnownEnglish(english);
        var thaiKnown = LanguageConverter.IsKnownThai(thai);
        if (source == NativeMethods.KeyboardLanguage.Thai && !thaiKnown && englishKnown)
            return new CorrectionPair(thai, english);
        if (source == NativeMethods.KeyboardLanguage.English && !englishKnown && thaiKnown)
            return new CorrectionPair(english, thai);
        if (source == NativeMethods.KeyboardLanguage.Unknown && englishKnown != thaiKnown)
            return englishKnown ? new CorrectionPair(thai, english) : new CorrectionPair(english, thai);
        return null;
    }

    internal static CorrectionPair? ChooseImmediateAutomatic(string english, string thai, NativeMethods.KeyboardLanguage source)
    {
        if (english.Length is < 3 or > 24 || thai.Length != english.Length) return null;
        var englishExact = LanguageConverter.IsExactKnownEnglish(english);
        var thaiExact = LanguageConverter.IsExactKnownThai(thai);
        if (source == NativeMethods.KeyboardLanguage.Thai && !thaiExact && englishExact)
            return new CorrectionPair(thai, english);
        if (source == NativeMethods.KeyboardLanguage.English && !englishExact && thaiExact)
            return new CorrectionPair(english, thai);
        if (source == NativeMethods.KeyboardLanguage.Unknown && englishExact != thaiExact)
            return englishExact ? new CorrectionPair(thai, english) : new CorrectionPair(english, thai);
        return null;
    }

    private void ClearBuffers()
    {
        englishToken.Clear();
        thaiToken.Clear();
        sourceLanguage = NativeMethods.KeyboardLanguage.Unknown;
    }

    private NativeMethods.KeyboardLanguage ResolveActiveLanguage()
    {
        var actual = NativeMethods.GetActiveKeyboardLanguage();
        if (expectedLanguage == NativeMethods.KeyboardLanguage.Unknown) return actual;
        if (actual == expectedLanguage || Environment.TickCount64 > expectedLanguageUntil)
        {
            expectedLanguage = NativeMethods.KeyboardLanguage.Unknown;
            return actual;
        }
        return expectedLanguage;
    }

    private void Correct(string original, string converted, char? delimiter, int? backspaceCount = null, bool allowUndo = true)
    {
        try
        {
            var targetWindow = NativeMethods.GetFocusedInputWindow();
            NativeMethods.SendBackspacesAndText(backspaceCount ?? original.Length, converted, delimiter);
            if (SwitchLayoutAfterCorrection)
            {
                var requested = NativeMethods.SwitchLayoutForText(targetWindow, converted);
                if (requested != NativeMethods.KeyboardLanguage.Unknown)
                {
                    expectedLanguage = requested;
                    expectedLanguageUntil = Environment.TickCount64 + ExpectedLanguageWindowMs;
                }
            }
            lock (gate)
                pendingUndo = allowUndo ? new UndoCorrection(original, converted, delimiter, targetWindow) : null;
            // Notifications touch UI (balloon tips); keep them out of the hook callback.
            uiContext.Post(_ => CorrectionMade?.Invoke(original, converted), null);
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex);
            uiContext.Post(_ => CorrectionFailed?.Invoke(ex), null);
        }
    }

    internal static bool IsWordBoundary(char character) => char.IsWhiteSpace(character);
    internal static bool IsAutomaticBoundaryKey(uint virtualKey) => virtualKey == NativeMethods.VkSpace;

    private async Task CorrectAfterShortcutAsync(
        string original,
        string converted,
        char? delimiter,
        int? backspaceCount,
        bool allowUndo)
    {
        for (var i = 0; i < 25 && AnyModifierDown(); i++)
            await Task.Delay(20).ConfigureAwait(false);
        uiContext.Post(_ => Correct(original, converted, delimiter, backspaceCount, allowUndo), null);
    }

    private static bool AnyModifierDown() =>
        (NativeMethods.GetAsyncKeyState(NativeMethods.VkShift) & 0x8000) != 0
        || (NativeMethods.GetAsyncKeyState(NativeMethods.VkControl) & 0x8000) != 0
        || (NativeMethods.GetAsyncKeyState(NativeMethods.VkMenu) & 0x8000) != 0;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        hookHealthTimer.Stop();
        hookHealthTimer.Dispose();
        if (hook != 0) NativeMethods.UnhookWindowsHookEx(hook);
        hook = 0;
    }
}
