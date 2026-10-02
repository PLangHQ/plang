namespace app.module.screen.type.screen.display.code;

/// <summary>The pointer holds a window: moves drag it until the button comes up. The window follows
/// the pointer once a tick, to where it is by then — moves in between (the mouse sends hundreds a
/// second) are passed over, so a drag never falls behind the pointer.</summary>
internal abstract class Grab(Window window)
{
    private Point? pointer;   // where the pointer went since the last tick

    protected Window Window { get; } = window;

    internal void Drag(Point at) => pointer = at;

    /// <summary>Once a tick: the window follows the pointer's latest place.</summary>
    internal void Tick()
    {
        if (pointer is not { } at) return;
        pointer = null;
        Follow(at);
    }

    protected abstract void Follow(Point at);

    /// <summary>The button came up: the window goes where the pointer last was.</summary>
    internal virtual void Release() => Tick();

    /// <summary>A window drew itself during the grab.</summary>
    internal virtual void Committed(Window drawn) { }
}

/// <summary>Moving a window by its title bar: it follows the pointer from where it was pressed.</summary>
internal sealed class MoveGrab(Window window, Point from) : Grab(window)
{
    private readonly Point start = window.At;

    protected override void Follow(Point at) => Window.MoveTo(start + (at - from));
}

/// <summary>Resizing a window by an edge (top 1, bottom 2, left 4, right 8): the client is asked
/// for each size, and the opposite edge stays put as it redraws.</summary>
internal sealed class ResizeGrab(Window window, uint edges, Point from) : Grab(window)
{
    private readonly Rect start = window.Frame;

    protected override void Follow(Point at)
    {
        var d = at - from;
        int w = start.Width, h = start.Height;
        if ((edges & 4) != 0) w = start.Width - d.X;
        if ((edges & 8) != 0) w = start.Width + d.X;
        if ((edges & 1) != 0) h = start.Height - d.Y;
        if ((edges & 2) != 0) h = start.Height + d.Y;
        Window.ResizeTo(new Size(w, h), going: true);
    }

    internal override void Release()
    {
        base.Release();
        Window.ResizeTo(Window.Size, going: false);
    }

    internal override void Committed(Window drawn)
    {
        if (ReferenceEquals(drawn, Window)) drawn.Anchor(start, edges);
    }
}
