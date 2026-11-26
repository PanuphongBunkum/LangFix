using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LangFix;

internal static class NativeMethods
{
    internal enum KeyboardLanguage { Unknown, English, Thai }
    internal const int WhKeyboardLl = 13;
    internal const int WmKeyDown = 0x0100;
    internal const int WmSysKeyDown = 0x0104;
    internal const int WmKeyUp = 0x0101;
    internal const int VkBack = 0x08;
    internal const int VkShift = 0x10;
    internal const int VkControl = 0x11;
    internal const int VkMenu = 0x12;
    internal const int VkSpace = 0x20;
    internal const int VkCapital = 0x14;
    internal const uint ToUnicodeNoStateChange = 0x4;
    internal const int VkLShift = 0xA0;
    internal const int VkRShift = 0xA1;
    internal const int VkLControl = 0xA2;
    internal const int VkRControl = 0xA3;
    internal const int VkLMenu = 0xA4;
    internal const int VkRMenu = 0xA5;
    internal const int LlkhfInjected = 0x10;
    internal const uint WmInputLangChangeRequest = 0x0050;
    internal const uint KlfActivate = 0x00000001;
    internal const uint InputKeyboard = 1;
    internal const uint KeyeventfKeyup = 0x0002;
    internal const uint KeyeventfUnicode = 0x0004;
    internal static readonly nint LangFixInjectionMarker = 0x4C4658;

    internal delegate nint HookProc(int code, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct KbdLlHookStruct { public uint vkCode, scanCode, flags, time; public nint dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Input { public uint type; public InputUnion U; }
    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] public KeybdInput ki;
        // INPUT is a union. Including its largest member is required so cbSize is
        // 40 bytes on x64 (32-bit union + 8-byte aligned type header).
        [FieldOffset(0)] public MouseInput mi;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct KeybdInput { public ushort wVk, wScan; public uint dwFlags, time; public nint dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput { public int dx, dy; public uint mouseData, dwFlags, time; public nint dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct LastInputInfo { public uint cbSize, dwTime; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct GuiThreadInfo
    {
        public uint cbSize, flags;
        public nint hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret;
        public Rect rcCaret;
    }

    [DllImport("user32.dll", SetLastError = true)] internal static extern nint SetWindowsHookEx(int idHook, HookProc callback, nint module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern short GetKeyState(int key);
    [DllImport("user32.dll")] internal static extern bool GetLastInputInfo(ref LastInputInfo info);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);
    [DllImport("user32.dll")] internal static extern bool GetKeyboardState(byte[] state);
    [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] internal static extern bool AttachThreadInput(uint attach, uint attachTo, bool enable);
    [DllImport("user32.dll")] internal static extern nint SetFocus(nint window);
    [DllImport("user32.dll")] internal static extern nint GetKeyboardLayout(uint threadId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint LoadKeyboardLayout(string layoutId, uint flags);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int ToUnicodeEx(uint vk, uint scan, byte[] state,
        [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder buffer, int count, uint flags, nint layout);
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] internal static extern uint MapVirtualKey(uint code, uint mapType);
    [DllImport("user32.dll")] internal static extern uint MapVirtualKeyEx(uint code, uint mapType, nint layout);

    internal static nint InstallHook(HookProc proc)
    {
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        var hook = SetWindowsHookEx(WhKeyboardLl, proc, GetModuleHandle(module.ModuleName), 0);
        if (hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        return hook;
    }

    internal static string TranslateKey(uint vk, uint scan, byte[] state, KeyboardLanguage language)
    {
        var buffer = new StringBuilder(8);
        var layout = GetInstalledKeyboardLayout(language);
        if (layout == 0) return string.Empty;
        if (scan == 0) scan = MapVirtualKey(vk, 0);
        var layoutVirtualKey = MapVirtualKeyEx(scan, 3, layout);
        if (layoutVirtualKey == 0) layoutVirtualKey = vk;
        // Flag 0x4: do not change kernel keyboard state (dead keys), so translating in the
        // hook never alters what the target application receives (Windows 10 1607+).
        var count = ToUnicodeEx(layoutVirtualKey, scan, state, buffer, buffer.Capacity, ToUnicodeNoStateChange, layout);
        return count > 0 ? buffer.ToString(0, count) : string.Empty;
    }

    internal static string TranslateSequenceForTesting(KeyboardLanguage language, params int[] virtualKeys)
    {
        var result = new StringBuilder();
        var state = new byte[256];
        foreach (var key in virtualKeys)
            result.Append(TranslateKey((uint)key, MapVirtualKey((uint)key, 0), state, language));
        return result.ToString();
    }

    internal static nint GetInstalledKeyboardLayout(KeyboardLanguage language)
    {
        foreach (InputLanguage installed in InputLanguage.InstalledInputLanguages)
        {
            var iso = installed.Culture.TwoLetterISOLanguageName;
            if (language == KeyboardLanguage.Thai && iso.Equals("th", StringComparison.OrdinalIgnoreCase))
                return installed.Handle;
            if (language == KeyboardLanguage.English && installed.Culture.Name.Equals("en-US", StringComparison.OrdinalIgnoreCase))
                return installed.Handle;
        }
        foreach (InputLanguage installed in InputLanguage.InstalledInputLanguages)
        {
            if (language == KeyboardLanguage.English && installed.Culture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase))
                return installed.Handle;
        }
        return LoadKeyboardLayout(language == KeyboardLanguage.Thai ? "0000041E" : "00000409", 0);
    }

    /// <summary>
    /// Builds the key state used for translation without touching other threads.
    /// This runs inside the low-level hook on every key press, so it must stay cheap:
    /// AttachThreadInput/SetFocus here can stall input system-wide and get the hook
    /// silently removed by Windows (LowLevelHooksTimeout).
    /// </summary>
    internal static byte[] GetActiveKeyboardState(bool capsLockOn)
    {
        var state = new byte[256];
        if (capsLockOn) state[VkCapital] = 0x01;
        SetPressedState(state, VkShift);
        SetPressedState(state, VkLShift);
        SetPressedState(state, VkRShift);
        SetPressedState(state, VkControl);
        SetPressedState(state, VkLControl);
        SetPressedState(state, VkRControl);
        SetPressedState(state, VkMenu);
        SetPressedState(state, VkLMenu);
        SetPressedState(state, VkRMenu);
        return state;
    }

    private static void SetPressedState(byte[] state, int virtualKey) =>
        state[virtualKey] = (byte)(((GetAsyncKeyState(virtualKey) & 0x8000) != 0) ? 0x80 : 0x00);

    /// <summary>
    /// Reads the Caps Lock toggle of the focused window's input queue. Attaching to another
    /// thread is relatively expensive, so callers only do this at startup and when focus changes.
    /// </summary>
    internal static bool ReadCapsLockState()
    {
        var focused = GetFocusedInputWindow();
        var focusedThread = focused != 0 ? GetWindowThreadProcessId(focused, out _) : 0;
        var currentThread = GetCurrentThreadId();
        var attached = focusedThread != 0 && focusedThread != currentThread && AttachThreadInput(currentThread, focusedThread, true);
        try
        {
            return (GetKeyState(VkCapital) & 0x0001) != 0;
        }
        finally
        {
            if (attached) AttachThreadInput(currentThread, focusedThread, false);
        }
    }

    /// <summary>Tick count (GetTickCount) of the most recent keyboard or mouse input anywhere in the session.</summary>
    internal static uint GetLastInputTick()
    {
        var info = new LastInputInfo { cbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
        return GetLastInputInfo(ref info) ? info.dwTime : 0;
    }

    internal static nint GetFocusedInputWindow()
    {
        var foreground = GetForegroundWindow();
        if (foreground == 0) return 0;
        var threadId = GetWindowThreadProcessId(foreground, out _);
        var info = new GuiThreadInfo { cbSize = (uint)Marshal.SizeOf<GuiThreadInfo>() };
        return GetGUIThreadInfo(threadId, ref info) && info.hwndFocus != 0 ? info.hwndFocus : foreground;
    }

    internal static void ForceForegroundForTesting(nint window, nint control)
    {
        var foreground = GetForegroundWindow();
        var foregroundThread = foreground != 0 ? GetWindowThreadProcessId(foreground, out _) : 0;
        var currentThread = GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != currentThread
            && AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            SetForegroundWindow(window);
            SetFocus(control);
        }
        finally
        {
            if (attached) AttachThreadInput(currentThread, foregroundThread, false);
        }
    }

    internal static nint GetKeyboardLayoutForFocusedControl()
    {
        var focused = GetFocusedInputWindow();
        var threadId = focused != 0 ? GetWindowThreadProcessId(focused, out _) : 0;
        return GetKeyboardLayout(threadId);
    }

    internal static KeyboardLanguage GetActiveKeyboardLanguage()
    {
        var languageId = (ushort)(GetKeyboardLayoutForFocusedControl().ToInt64() & 0xFFFF);
        if (languageId == 0x041E) return KeyboardLanguage.Thai;
        if ((languageId & 0x03FF) == 0x0009) return KeyboardLanguage.English;
        return KeyboardLanguage.Unknown;
    }

    internal static void SendBackspacesAndText(int backspaces, string text, char? delimiter = null)
    {
        var inputs = new List<Input>();
        for (var i = 0; i < backspaces; i++) AddVirtualKey(inputs, VkBack);
        foreach (var c in text) AddUnicode(inputs, c);
        if (delimiter is char d) AddUnicode(inputs, d);
        if (inputs.Count > 0 && SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Input>()) != inputs.Count)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    internal static void SendTestVirtualKeys(params int[] virtualKeys)
    {
        var inputs = new List<Input>();
        foreach (var key in virtualKeys)
        {
            var scan = (ushort)MapVirtualKey((uint)key, 0);
            inputs.Add(new Input { type = InputKeyboard, U = new InputUnion { ki = new KeybdInput { wVk = (ushort)key, wScan = scan } } });
            inputs.Add(new Input { type = InputKeyboard, U = new InputUnion { ki = new KeybdInput { wVk = (ushort)key, wScan = scan, dwFlags = KeyeventfKeyup } } });
        }
        if (SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Input>()) != inputs.Count)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    internal static void SendTestKeyState(int virtualKey, bool keyUp)
    {
        var scan = (ushort)MapVirtualKey((uint)virtualKey, 0);
        var input = new Input
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KeybdInput
                {
                    wVk = (ushort)virtualKey,
                    wScan = scan,
                    dwFlags = keyUp ? KeyeventfKeyup : 0
                }
            }
        };
        if (SendInput(1, [input], Marshal.SizeOf<Input>()) != 1)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    internal static string TargetLayoutId(string text) => text.Any(c => c is >= '\u0E00' and <= '\u0E7F')
        ? "0000041E" // Thai Kedmanee
        : "00000409"; // English (United States)

    /// <summary>
    /// Asks the target window to switch layout and returns immediately (no waiting, because this
    /// can run inside the keyboard hook). Returns the language requested, or Unknown when nothing
    /// was requested. The switch is applied asynchronously by the target thread.
    /// </summary>
    internal static KeyboardLanguage SwitchLayoutForText(nint targetWindow, string text)
    {
        if (targetWindow == 0) return KeyboardLanguage.Unknown;
        var targetLanguage = TargetLayoutId(text) == "0000041E" ? KeyboardLanguage.Thai : KeyboardLanguage.English;
        var layout = GetInstalledKeyboardLayout(targetLanguage);
        if (layout == 0) return KeyboardLanguage.Unknown;
        var targetThread = GetWindowThreadProcessId(targetWindow, out _);
        if (GetKeyboardLayout(targetThread) == layout) return KeyboardLanguage.Unknown;
        var flags = TargetLayoutId(text) == "00000409" ? 2 : 3;
        return PostMessage(targetWindow, WmInputLangChangeRequest, flags, layout) ? targetLanguage : KeyboardLanguage.Unknown;
    }

    private static void AddVirtualKey(List<Input> inputs, int vk)
    {
        inputs.Add(new Input { type = InputKeyboard, U = new InputUnion { ki = new KeybdInput { wVk = (ushort)vk, dwExtraInfo = LangFixInjectionMarker } } });
        inputs.Add(new Input { type = InputKeyboard, U = new InputUnion { ki = new KeybdInput { wVk = (ushort)vk, dwFlags = KeyeventfKeyup, dwExtraInfo = LangFixInjectionMarker } } });
    }

    private static void AddUnicode(List<Input> inputs, char c)
    {
        inputs.Add(new Input { type = InputKeyboard, U = new InputUnion { ki = new KeybdInput { wScan = c, dwFlags = KeyeventfUnicode, dwExtraInfo = LangFixInjectionMarker } } });
        inputs.Add(new Input { type = InputKeyboard, U = new InputUnion { ki = new KeybdInput { wScan = c, dwFlags = KeyeventfUnicode | KeyeventfKeyup, dwExtraInfo = LangFixInjectionMarker } } });
    }
}
