namespace app.module.screen.code.wayland;

// The input protocol objects. They only speak: the seat's Pointer and Keyboard decide.

/// <summary>wl_seat: one seat, with a pointer and a keyboard.</summary>
internal sealed class WlSeat : Resource
{
    internal WlSeat(Client client, uint id, uint version) : base(client, id, version)
    {
        Event(0).Uint(3).Send();   // pointer | keyboard
        if (Version >= 2) Event(1).String("seat0").Send();
    }

    internal override void Request(ushort opcode, Args args)
    {
        switch (opcode)
        {
            case 0: _ = new WlPointer(Client, args.NewId(), Version); break;
            case 1: _ = new WlKeyboard(Client, args.NewId(), Version); break;
            case 3: Destroy(); break;
        }
    }
}

/// <summary>wl_pointer: one client's view of the seat's pointer.</summary>
internal sealed class WlPointer : Resource
{
    internal WlPointer(Client client, uint id, uint version) : base(client, id, version) => Display.Pointer.Add(this);

    internal override void Request(ushort opcode, Args args)
    {
        switch (opcode)
        {
            case 0:   // set_cursor: a surface of its own, or none
                args.Uint();
                Display.Pointer.Shape(args.Object<WlSurface>() == null ? "none" : "default");
                break;
            case 1: Destroy(); break;
        }
    }

    protected override void Destroyed() => Display.Pointer.Remove(this);

    internal void Enter(uint serial, WlSurface surface, double x, double y) => Event(0).Uint(serial).Object(surface).Fixed(x).Fixed(y).Send();
    internal void Leave(uint serial, WlSurface surface) => Event(1).Uint(serial).Object(surface).Send();
    internal void Motion(uint time, double x, double y) => Event(2).Uint(time).Fixed(x).Fixed(y).Send();
    internal void Button(uint serial, uint time, uint button, bool pressed) => Event(3).Uint(serial).Uint(time).Uint(button).Uint(pressed ? 1u : 0u).Send();

    /// <summary>A wheel turn: <paramref name="value120"/> is 120 per notch; axis 0 vertical, 1 horizontal.</summary>
    internal void Wheel(uint time, uint axis, int value120)
    {
        if (Version >= 5) Event(6).Uint(0).Send();                        // axis_source: wheel
        if (Version >= 8) Event(9).Uint(axis).Int(value120).Send();       // axis_value120
        Event(4).Uint(time).Uint(axis).Fixed(value120 / 8.0).Send();      // axis
    }

    internal void Frame()
    {
        if (Version >= 5) Event(5).Send();
    }
}

/// <summary>wl_keyboard: one client's view of the seat's keyboard.</summary>
internal sealed class WlKeyboard : Resource
{
    internal WlKeyboard(Client client, uint id, uint version) : base(client, id, version) => Display.Keyboard.Add(this);

    internal override void Request(ushort opcode, Args args)
    {
        if (opcode == 0) Destroy();
    }

    protected override void Destroyed() => Display.Keyboard.Remove(this);

    internal void Keymap(int fd, int size) => Event(0).Uint(1).Fd(fd).Uint((uint)size).Send();   // xkb v1

    internal void Enter(uint serial, WlSurface surface, IEnumerable<uint> held)
    {
        var keys = held.SelectMany(BitConverter.GetBytes).ToArray();
        Event(1).Uint(serial).Object(surface).Array(keys).Send();
    }

    internal void Leave(uint serial, WlSurface surface) => Event(2).Uint(serial).Object(surface).Send();
    internal void Key(uint serial, uint time, uint key, bool pressed) => Event(3).Uint(serial).Uint(time).Uint(key).Uint(pressed ? 1u : 0u).Send();
    internal void Modifiers(uint serial, Modifiers m) => Event(4).Uint(serial).Uint(m.Depressed).Uint(m.Latched).Uint(m.Locked).Uint(m.Group).Send();

    internal void Repeat(int rate, int delay)
    {
        if (Version >= 4) Event(5).Int(rate).Int(delay).Send();
    }
}

/// <summary>wp_cursor_shape_manager_v1: clients name the pointer they want (CSS names).</summary>
internal sealed class CursorShapeManager(Client client, uint id, uint version) : Resource(client, id, version)
{
    internal override void Request(ushort opcode, Args args)
    {
        switch (opcode)
        {
            case 0: Destroy(); break;
            case 1: _ = new CursorShapeDevice(Client, args.NewId()); break;
            case 2: args.NewId(); break;   // tablets: none here
        }
    }
}

internal sealed class CursorShapeDevice(Client client, uint id) : Resource(client, id, 1)
{
    // wp_cursor_shape_device_v1.shape: 1 = default … 36 = all-resize, as CSS names
    private static readonly string[] Names =
    [
        "", "default", "context-menu", "help", "pointer", "progress", "wait", "cell", "crosshair", "text",
        "vertical-text", "alias", "copy", "move", "no-drop", "not-allowed", "grab", "grabbing", "e-resize",
        "n-resize", "ne-resize", "nw-resize", "s-resize", "se-resize", "sw-resize", "w-resize", "ew-resize",
        "ns-resize", "nesw-resize", "nwse-resize", "col-resize", "row-resize", "all-scroll", "zoom-in",
        "zoom-out", "default", "move",
    ];

    internal override void Request(ushort opcode, Args args)
    {
        switch (opcode)
        {
            case 0: Destroy(); break;
            case 1:
                args.Uint();
                var shape = args.Uint();
                Display.Pointer.Shape(shape < Names.Length ? Names[shape] : "default");
                break;
        }
    }
}

/// <summary>What xkb says is held, latched and locked, and the layout group.</summary>
internal readonly record struct Modifiers(uint Depressed, uint Latched, uint Locked, uint Group);
