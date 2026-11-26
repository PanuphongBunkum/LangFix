namespace LangFix;

internal readonly record struct ShortcutDefinition(Keys Key, Keys Modifiers)
{
    private const Keys AllowedModifiers = Keys.Control | Keys.Alt | Keys.Shift;

    public static ShortcutDefinition Default => new(Keys.Back, Keys.Shift);

    public bool IsValid => Key != Keys.None && !IsModifierKey(Key) && (Modifiers & AllowedModifiers) != Keys.None;

    public bool Matches(uint virtualKey, bool controlDown, bool altDown, bool shiftDown)
    {
        if ((uint)Key != virtualKey) return false;
        return controlDown == Modifiers.HasFlag(Keys.Control)
            && altDown == Modifiers.HasFlag(Keys.Alt)
            && shiftDown == Modifiers.HasFlag(Keys.Shift);
    }

    public string DisplayText
    {
        get
        {
            var parts = new List<string>();
            if (Modifiers.HasFlag(Keys.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(Keys.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(Keys.Shift)) parts.Add("Shift");
            parts.Add(Key == Keys.Back ? "Backspace" : Key.ToString());
            return string.Join(" + ", parts);
        }
    }

    public static ShortcutDefinition FromSettings(int key, int modifiers)
    {
        var shortcut = new ShortcutDefinition((Keys)key, (Keys)modifiers & AllowedModifiers);
        return shortcut.IsValid ? shortcut : Default;
    }

    public static bool IsModifierKey(Keys key) => key is Keys.ControlKey or Keys.LControlKey or Keys.RControlKey
        or Keys.Menu or Keys.LMenu or Keys.RMenu or Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey;
}
