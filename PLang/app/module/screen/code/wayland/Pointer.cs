namespace app.module.screen.code.wayland;

/// <summary>
/// The seat's pointer: where it is, which client surface has it, the buttons held, the shape to
/// show. While a button is held, the surface it went down on keeps the pointer (a drag that leaves
/// the window still selects text, moves a scrollbar).
/// </summary>
internal sealed class Pointer(Display display)
{
    private readonly List<WlPointer> bindings = new();
    private readonly HashSet<uint> held = new();
    private WlSurface? focus;
    private Point corner;          // the focused surface's top-left on the screen
    private string shape = "default";
    private string? ours;          // plang-screen's own parts (title bar, edges, panels) show this
    private string? shown;

    internal Point At { get; private set; }
    internal Point Pressed { get; private set; }
    internal bool Holding => held.Count > 0;

    internal void Add(WlPointer binding) => bindings.Add(binding);
    internal void Remove(WlPointer binding) => bindings.Remove(binding);

    private IEnumerable<WlPointer> Of(WlSurface surface) => bindings.Where(b => b.Client == surface.Client && b.Alive).ToList();

    /// <summary>The pointer is at <paramref name="at"/>, over <paramref name="target"/> (a client
    /// surface and its corner), or over plang-screen's own parts (null).</summary>
    internal void Move(Point at, Target? target)
    {
        At = at;
        if (!Holding) Focus(target);
        if (focus != null)
            foreach (var b in Of(focus)) b.Motion(display.Time(), at.X - corner.X, at.Y - corner.Y);
        Frame();
    }

    private void Focus(Target? target)
    {
        if (target is { } t && ReferenceEquals(t.Surface, focus))
        {
            corner = t.Corner;
            return;
        }
        if (focus != null)
            foreach (var b in Of(focus)) b.Leave(display.Serial(), focus);
        focus = target?.Surface;
        corner = target?.Corner ?? default;
        if (focus == null) return;
        foreach (var b in Of(focus)) b.Enter(display.Serial(), focus, At.X - corner.X, At.Y - corner.Y);
    }

    /// <summary>Lets go of every surface (a window is being moved or resized).</summary>
    internal void Leave()
    {
        Focus(null);
        Frame();
    }

    internal void Button(uint button, bool pressed)
    {
        if (pressed) { held.Add(button); Pressed = At; }
        else held.Remove(button);
        if (focus != null)
            foreach (var b in Of(focus)) b.Button(display.Serial(), display.Time(), button, pressed);
        Frame();
    }

    internal void Wheel(int dx, int dy)
    {
        if (focus == null) return;
        foreach (var b in Of(focus))
        {
            if (dy != 0) b.Wheel(display.Time(), 0, dy);
            if (dx != 0) b.Wheel(display.Time(), 1, dx);
        }
        Frame();
    }

    private void Frame()
    {
        if (focus != null)
            foreach (var b in Of(focus)) b.Frame();
    }

    /// <summary>The shape a client asks for (over its own surface).</summary>
    internal void Shape(string name)
    {
        shape = name;
        Show();
    }

    /// <summary>plang-screen's own shape over its parts; null gives the pointer back to the client.</summary>
    internal void Ours(string? name)
    {
        ours = name;
        Show();
    }

    private void Show()
    {
        var name = ours ?? shape;
        if (name == shown) return;
        shown = name;
        display.Frame.Cursor(name);
    }
}

/// <summary>A client surface under the pointer, and where its top-left is on the screen.</summary>
internal readonly record struct Target(WlSurface Surface, Point Corner);
