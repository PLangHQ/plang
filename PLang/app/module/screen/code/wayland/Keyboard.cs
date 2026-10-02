namespace app.module.screen.code.wayland;

/// <summary>
/// The seat's keyboard: the keyboard map (xkb, the layout PlangOS types in), the keys held, and
/// which client surface has the keyboard. Keys are Linux evdev codes; the host sends Windows
/// scancodes, which <see cref="Evdev"/> turns into them.
/// </summary>
internal sealed class Keyboard
{
    private readonly Display display;
    private readonly List<WlKeyboard> bindings = new();
    private readonly HashSet<uint> held = new();
    private readonly byte[] keymap;    // the map's text, NUL-terminated, as clients read it
    private readonly IntPtr state;     // xkb's own state: what the held keys mean (Shift, AltGr …)
    private Modifiers modifiers;

    internal WlSurface? Focus { get; private set; }

    internal Keyboard(Display display, string layout)
    {
        this.display = display;
        var (map, text) = Native.Keymap(layout);
        keymap = System.Text.Encoding.UTF8.GetBytes(text + "\0");
        state = map == IntPtr.Zero ? IntPtr.Zero : Native.xkb_state_new(map);
    }

    internal void Add(WlKeyboard binding)
    {
        bindings.Add(binding);
        binding.Keymap(Native.Memory("keymap", keymap), keymap.Length);
        binding.Repeat(30, 400);   // the client repeats a held key
        // (enter names the surface: never one the client destroyed)
        if (Focus is { Alive: true } && Focus.Client == binding.Client)
        {
            binding.Enter(display.Serial(), Focus, held);
            binding.Modifiers(display.Serial(), modifiers);
        }
    }

    internal void Remove(WlKeyboard binding) => bindings.Remove(binding);

    private IEnumerable<WlKeyboard> Of(WlSurface surface) => bindings.Where(b => b.Client == surface.Client && b.Alive).ToList();

    /// <summary>The keyboard goes to <paramref name="surface"/> (a window), and so does the clipboard.</summary>
    internal void Give(WlSurface? surface)
    {
        if (ReferenceEquals(surface, Focus)) return;
        if (Focus is { Alive: true } old)
            foreach (var b in Of(old)) b.Leave(display.Serial(), old);
        Focus = surface;
        if (surface != null)
            foreach (var b in Of(surface))
            {
                b.Enter(display.Serial(), surface, held);
                b.Modifiers(display.Serial(), modifiers);
            }
        display.Clipboard.Offer(surface?.Client);
    }

    /// <summary>A key, down or up. The host repeats a held key; the client does that itself, so a
    /// second "down" is dropped.</summary>
    internal void Key(uint key, bool pressed)
    {
        if (pressed ? !held.Add(key) : !held.Remove(key)) return;
        if (state != IntPtr.Zero) Native.xkb_state_update_key(state, key + 8, pressed ? 1 : 0);
        if (Focus == null) return;
        foreach (var b in Of(Focus)) b.Key(display.Serial(), display.Time(), key, pressed);
        var now = Serialized();
        if (now == modifiers) return;
        modifiers = now;
        foreach (var b in Of(Focus)) b.Modifiers(display.Serial(), now);
    }

    /// <summary>A shortcut: <paramref name="key"/> with <paramref name="mods"/> held around it.</summary>
    internal void Press(uint key, params uint[] mods)
    {
        foreach (var m in mods) Key(m, true);
        Key(key, true);
        Key(key, false);
        foreach (var m in mods.Reverse()) Key(m, false);
    }

    private Modifiers Serialized() => state == IntPtr.Zero ? default : new Modifiers(
        Native.xkb_state_serialize_mods(state, 1), Native.xkb_state_serialize_mods(state, 2),
        Native.xkb_state_serialize_mods(state, 4), Native.xkb_state_serialize_layout(state, 0x80));

    // evdev codes the menus and title bar press
    internal const uint LeftAlt = 56, LeftCtrl = 29, Left = 105, Right = 106, F5 = 63, F12 = 88,
        F = 33, P = 25, Zero = 11, KeypadPlus = 78, KeypadMinus = 74;

    /// <summary>A Windows (PS/2 set 1) scancode as a Linux evdev code: plain keys are the same number,
    /// extended (E0) keys differ.</summary>
    internal static uint? Evdev(uint scancode, bool extended)
    {
        if (scancode == 0) return null;
        if (!extended) return scancode;
        return scancode switch
        {
            0x1C => 96, 0x1D => 97, 0x35 => 98, 0x38 => 100,        // keypad Enter, right Ctrl, keypad /, AltGr
            0x47 => 102, 0x48 => 103, 0x49 => 104, 0x4B => 105,     // Home, Up, Page Up, Left
            0x4D => 106, 0x4F => 107, 0x50 => 108, 0x51 => 109,     // Right, End, Down, Page Down
            0x52 => 110, 0x53 => 111, 0x5B => 125, 0x5C => 126, 0x5D => 127,   // Insert, Delete, Windows, Menu
            _ => scancode,
        };
    }
}
