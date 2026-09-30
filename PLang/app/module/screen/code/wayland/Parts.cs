namespace app.module.screen.code.wayland;

/// <summary>A mouse button on the screen: which (evdev code) and how many clicks in a row.</summary>
internal readonly record struct Click(uint Button, int Clicks);

/// <summary>
/// What is under the pointer. A client's surface takes the pointer itself (<see cref="Target"/>);
/// plang-screen's own parts — title bar, edges, panels — show their <see cref="Cursor"/> and answer
/// clicks themselves.
/// </summary>
internal interface IPart
{
    /// <summary>The client surface the pointer goes to; null over plang-screen's own parts.</summary>
    Target? Target => null;
    /// <summary>The pointer shape plang-screen shows here; null leaves it to the client.</summary>
    string? Cursor => "default";
    void Down(Click click) { }
    void Up(Click click) { }
    /// <summary>The pointer is here (it moved over it, or on it).</summary>
    void Over(Point at) { }
    /// <summary>The pointer left.</summary>
    void Out() { }
}

/// <summary>A client's surface: a window's page, a menu, the desktop. A click brings its window
/// forward; the button itself goes to the client.</summary>
internal sealed class Content(Window? window, Target target) : IPart
{
    public Target? Target => target;
    public string? Cursor => null;
    public void Down(Click click) => window?.Activate();
}

/// <summary>A title bar's empty middle: drag to move, double-click to maximize.</summary>
internal sealed class TitleArea(Window window) : IPart
{
    public void Down(Click click)
    {
        window.Activate();
        if (click.Clicks >= 2) window.Toggle();
        else if (window.Shown == Shown.Normal) window.Display.Hold(new MoveGrab(window, window.Display.Pointer.At));
    }
}

/// <summary>Just outside a window: drag to resize. Edges: top 1, bottom 2, left 4, right 8.</summary>
internal sealed class Edge(Window window, uint edges) : IPart
{
    public string? Cursor => edges switch
    {
        1 or 2 => "ns-resize",
        4 or 8 => "ew-resize",
        5 or 10 => "nwse-resize",
        _ => "nesw-resize",
    };

    public void Down(Click click)
    {
        window.Activate();
        window.Display.Hold(new ResizeGrab(window, edges, window.Display.Pointer.At));
    }
}

/// <summary>
/// A title bar button. It acts when the mouse button comes up on it (the one it went down on);
/// it draws its own icon. Left ones sit at fixed places; right ones count from the bar's end.
/// </summary>
internal abstract class Button(TitleBar bar) : IPart
{
    internal const int Side = 34;      // back, forward, address, menu
    internal const int Caption = 46;   // minimize, maximize, close

    protected TitleBar Bar { get; } = bar;
    protected Window Window => Bar.Window;

    /// <summary>Where it is in a bar <paramref name="width"/> wide: (x, width).</summary>
    internal abstract (int x, int width) Span(int width);

    internal bool Holds(int x, int width)
    {
        var (at, w) = Span(width);
        return x >= at && x < at + w;
    }

    /// <summary>Its hover background.</summary>
    internal virtual void Highlight(Canvas c, int width)
    {
        var (x, w) = Span(width);
        c.Round(new Rect(x + 2, 5, w - 4, TitleBar.Height - 10), 6, Color.Hover);
    }

    /// <summary>Its icon, centred on (cx, mid).</summary>
    internal abstract void Paint(Canvas c, float cx, float mid, Color ink);

    protected abstract void Act();

    public void Down(Click click)
    {
        Window.Activate();
        Window.Display.Pressed = this;
    }

    public void Up(Click click)
    {
        if (!ReferenceEquals(Window.Display.Pressed, this)) return;
        Window.Display.Pressed = null;
        Act();
    }

    public void Over(Point at) => Bar.Hover(this);
    public void Out() => Bar.Hover(null);
}

/// <summary>‹ Back: the key Chromium knows for it (Alt+Left).</summary>
internal sealed class BackButton(TitleBar bar) : Button(bar)
{
    internal override (int, int) Span(int width) => (6, Side);
    internal override void Paint(Canvas c, float cx, float mid, Color ink)
    {
        c.Line(cx + 2.5f, mid - 5, cx - 2.5f, mid, 1.4f, ink);
        c.Line(cx - 2.5f, mid, cx + 2.5f, mid + 5, 1.4f, ink);
    }
    protected override void Act() => Window.Press(Keyboard.Left, Keyboard.LeftAlt);
}

/// <summary>› Forward (Alt+Right).</summary>
internal sealed class ForwardButton(TitleBar bar) : Button(bar)
{
    internal override (int, int) Span(int width) => (6 + Side, Side);
    internal override void Paint(Canvas c, float cx, float mid, Color ink)
    {
        c.Line(cx - 2.5f, mid - 5, cx + 2.5f, mid, 1.4f, ink);
        c.Line(cx + 2.5f, mid, cx - 2.5f, mid + 5, 1.4f, ink);
    }
    protected override void Act() => Window.Press(Keyboard.Right, Keyboard.LeftAlt);
}

/// <summary>The globe: drops down the address field.</summary>
internal sealed class AddressButton(TitleBar bar) : Button(bar)
{
    internal override (int, int) Span(int width) => (6 + 2 * Side, Side);
    internal override void Paint(Canvas c, float cx, float mid, Color ink)
    {
        c.Ring(cx, mid, 7, 7, 1.2f, ink);
        c.Line(cx - 7, mid, cx + 7, mid, 1, ink);
        c.Ring(cx, mid, 3.2f, 7, 1, ink);
    }
    protected override void Act() => Window.Display.Open(new AddressField(Window));
}

/// <summary>☰ The window's menu: Chromium's tools.</summary>
internal sealed class MenuButton(TitleBar bar) : Button(bar)
{
    internal override (int, int) Span(int width) => (width - 3 * Caption - Side - 6, Side);
    internal override void Paint(Canvas c, float cx, float mid, Color ink)
    {
        foreach (var dy in new[] { -4.5f, 0.5f, 5.5f }) c.Line(cx - 6, mid + dy, cx + 6, mid + dy, 1.2f, ink);
    }
    protected override void Act()
    {
        if (Window.Display.Panel is WindowMenu open && open.Owner == Window) open.Close();
        else Window.Display.Open(new WindowMenu(Window));
    }
}

internal sealed class MinimizeButton(TitleBar bar) : Button(bar)
{
    internal override (int, int) Span(int width) => (width - 3 * Caption, Caption);
    internal override void Highlight(Canvas c, int width) { var (x, w) = Span(width); c.Fill(new Rect(x, 0, w, TitleBar.Height), Color.Hover); }
    internal override void Paint(Canvas c, float cx, float mid, Color ink) => c.Line(cx - 5, mid + 0.5f, cx + 5, mid + 0.5f, 1, ink);
    protected override void Act() => Window.Minimize();
}

/// <summary>□ maximize; ⧉ restore when maximized.</summary>
internal sealed class MaximizeButton(TitleBar bar) : Button(bar)
{
    internal override (int, int) Span(int width) => (width - 2 * Caption, Caption);
    internal override void Highlight(Canvas c, int width) { var (x, w) = Span(width); c.Fill(new Rect(x, 0, w, TitleBar.Height), Color.Hover); }
    internal override void Paint(Canvas c, float cx, float mid, Color ink)
    {
        // 1 px lines on pixel centers (.5): crisp
        if (Window.Shown == Shown.Maximized)
        {
            c.Outline(cx - 5.5f, mid - 2.5f, 8, 8, ink);
            c.Line(cx - 2.5f, mid - 5.5f, cx + 5.5f, mid - 5.5f, 1, ink);
            c.Line(cx + 5.5f, mid - 5.5f, cx + 5.5f, mid + 2.5f, 1, ink);
        }
        else c.Outline(cx - 4.5f, mid - 4.5f, 10, 10, ink);
    }
    protected override void Act() => Window.Toggle();
}

internal sealed class CloseButton(TitleBar bar) : Button(bar)
{
    internal override (int, int) Span(int width) => (width - Caption, Caption);
    internal override void Highlight(Canvas c, int width) { var (x, w) = Span(width); c.Fill(new Rect(x, 0, w, TitleBar.Height), Color.CloseHover); }
    internal override void Paint(Canvas c, float cx, float mid, Color ink)
    {
        var x = Bar.Hovered == this ? Color.Ink : ink;
        c.Line(cx - 5, mid - 5, cx + 5, mid + 5, 1.1f, x);
        c.Line(cx + 5, mid - 5, cx - 5, mid + 5, 1.1f, x);
    }
    protected override void Act() => Window.Toplevel.Close();
}

/// <summary>
/// The size grip in a window's bottom-right corner (as Windows 95 and Mac OS 9 had it): three
/// diagonal ridges over the corner of the page, where its scrollbars meet. Dragging it resizes the
/// window's width and height. Two-toned, so it shows on a light page and a dark one; not there while
/// the window is maximized.
/// </summary>
internal sealed class SizeGrip(Window window)
{
    internal const int Side = 16;
    private static readonly Color Dark = Color.Rgba(0x6b, 0x72, 0x80, 0.95f);
    private static readonly Color Light = Color.Rgba(0xff, 0xff, 0xff, 0.9f);

    internal Picture Picture { get; } = Paint();

    internal bool Holds(int x, int y) => window.Shown == Shown.Normal && Picture.Rect.Contains(x, y);

    internal void Place() => Picture.Place(new Point(window.At.X + window.Size.Width - Side, window.At.Y + window.Size.Height - Side));

    internal void Draw(int y, int x0, Span<byte> line)
    {
        if (window.Shown == Shown.Normal) Picture.Draw(y, x0, line);
    }

    private static Picture Paint()
    {
        var c = new Canvas(Side, Side);
        for (var i = 0; i < 3; i++)
        {
            var d = 4 + 4 * i;   // how far from the corner this ridge starts
            c.Line(Side - d, Side - 1, Side - 1, Side - d, 1.2f, Dark);
            c.Line(Side - d + 1.2f, Side - 1, Side - 1, Side - d + 1.2f, 1f, Light);
        }
        return c.Picture(default);
    }
}

/// <summary>
/// A window's title bar, drawn by plang-screen: back, forward, address; the title; menu, minimize,
/// maximize, close. It draws itself again when the window's title, width, activity or hover changes.
/// </summary>
internal sealed class TitleBar
{
    internal const int Height = 36;
    private readonly Button[] buttons;
    private readonly TitleArea area;

    internal Window Window { get; }
    internal Button? Hovered { get; private set; }
    internal Picture Picture { get; private set; } = Picture.None;

    internal TitleBar(Window window)
    {
        Window = window;
        area = new TitleArea(window);
        buttons = [new BackButton(this), new ForwardButton(this), new AddressButton(this), new MenuButton(this),
                   new MinimizeButton(this), new MaximizeButton(this), new CloseButton(this)];
    }

    /// <summary>The part at <paramref name="x"/> (from the bar's left).</summary>
    internal IPart Hit(int x) => buttons.FirstOrDefault(b => b.Holds(x, Window.Size.Width)) ?? (IPart)area;

    internal void Hover(Button? button)
    {
        if (ReferenceEquals(button, Hovered)) return;
        Hovered = button;
        Draw();
    }

    internal void Place() => Picture.Place(Corner);

    private Point Corner => new(Window.At.X, Window.At.Y - Height);

    internal void Draw()
    {
        var width = Window.Size.Width;
        var active = Window.Active;
        var ink = active ? Color.Ink : Color.InkInactive;
        var c = new Canvas(width, Height);
        c.Round(new Rect(0, 0, width, Height), Window.Shown == Shown.Maximized ? 0 : 8, active ? Color.BarActive : Color.BarInactive, topOnly: true);
        Hovered?.Highlight(c, width);
        const float mid = Height / 2f;
        foreach (var b in buttons)
        {
            var (x, w) = b.Span(width);
            b.Paint(c, x + w / 2f, mid, ink);
        }
        var (menuX, _) = buttons.OfType<MenuButton>().First().Span(width);
        c.Text(Window.Display.Font, Window.Toplevel.Title, 6 + 3 * Button.Side + 10, mid, menuX - 10, ink);
        var old = Picture.Rect;
        Picture = c.Picture(Corner);
        if (Window.Visible) Window.Display.Frame.Redraw(old, Picture.Rect);
    }
}
