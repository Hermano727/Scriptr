namespace Scriptr.Gui;

internal sealed class HotkeyBinding
{
    public Keys Key       { get; }
    public Keys Modifiers { get; }   // Keys.Control | Keys.Shift | Keys.Alt, or Keys.None

    public HotkeyBinding(Keys key, Keys modifiers = Keys.None)
    {
        Key       = key;
        Modifiers = modifiers;
    }

    // MOD_NOREPEAT (0x4000) is always set — prevents the hotkey from auto-repeating
    // while the key is held, which would toggle record/play dozens of times per second.
    internal uint Win32Modifiers
    {
        get
        {
            uint m = 0x4000u;
            if ((Modifiers & Keys.Control) != 0) m |= 0x0002u;
            if ((Modifiers & Keys.Alt)     != 0) m |= 0x0001u;
            if ((Modifiers & Keys.Shift)   != 0) m |= 0x0004u;
            return m;
        }
    }

    internal ushort Win32Vk => (ushort)Key;

    public string DisplayText
    {
        get
        {
            var parts = new List<string>(4);
            if ((Modifiers & Keys.Control) != 0) parts.Add("Ctrl");
            if ((Modifiers & Keys.Alt)     != 0) parts.Add("Alt");
            if ((Modifiers & Keys.Shift)   != 0) parts.Add("Shift");
            parts.Add(FriendlyName(Key));
            return string.Join("+", parts);
        }
    }

    private static string FriendlyName(Keys k) => k switch
    {
        Keys.F1  => "F1",  Keys.F2  => "F2",  Keys.F3  => "F3",  Keys.F4  => "F4",
        Keys.F5  => "F5",  Keys.F6  => "F6",  Keys.F7  => "F7",  Keys.F8  => "F8",
        Keys.F9  => "F9",  Keys.F10 => "F10", Keys.F11 => "F11", Keys.F12 => "F12",
        Keys.Pause  => "Pause",
        Keys.Escape => "Esc",
        Keys.Insert => "Ins",
        Keys.Delete => "Del",
        Keys.Home   => "Home",
        Keys.End    => "End",
        _           => k.ToString()
    };
}
