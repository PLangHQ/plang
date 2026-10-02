namespace app.module.screen.type.screen.display.code;

/// <summary>
/// The windows, bottom to top — the desktop always at the bottom — and which one is active (has
/// the keyboard, its title bar lit). Owns stacking, activation, and what is under a point.
/// </summary>
internal sealed class Windows(Display display)
{
    /// <summary>The desktop's taskbar height: the strip above every window, which maximized windows leave free.</summary>
    internal const int Taskbar = 56;

    private readonly List<Window> list = new();
    private int next;

    internal Window? Active { get; private set; }
    internal Window? Desktop => list.FirstOrDefault(w => w.Desktop);

    /// <summary>Where windows go (title bars included): the screen above the taskbar.</summary>
    internal Rect Work => new(0, 0, display.Size.Width, display.Size.Height - Taskbar);
    private Rect TaskbarRect => new(0, display.Size.Height - Taskbar, display.Size.Width, Taskbar);

    /// <summary>A new toplevel's window: the first is the desktop (the whole screen); the others
    /// cascade from the top left.</summary>
    internal Window Open(XdgToplevel toplevel)
    {
        var desktop = next == 0;
        var n = list.Count(w => !w.Desktop) % 8;
        var work = Work;
        var window = desktop
            ? new Window(display, toplevel, 0, true, new Point(0, 0), new Size(display.Size.Width, display.Size.Height))
            : new Window(display, toplevel, next, false, new Point(80 + 36 * n, 40 + TitleBar.Height + 36 * n),
                new Size(Math.Min(1280, work.Width - 240), Math.Min(800, work.Height - 160 - TitleBar.Height)));
        next++;
        list.Add(window);
        window.Tell("opened", new() { ["title"] = toplevel.Title, ["app"] = toplevel.App });
        Activate(window);
        return window;
    }

    internal void Close(Window window)
    {
        if (!list.Remove(window)) return;
        window.Tell("closed");
        display.Panel?.Closed(window);
        display.Frame.Redraw(window.Outer, default);
        if (ReferenceEquals(Active, window))
        {
            Active = null;
            ActivateTop();
        }
    }

    internal Window? Of(WlSurface surface) => list.FirstOrDefault(w => ReferenceEquals(w.Surface, surface));
    internal Window? ById(int id) => list.FirstOrDefault(w => w.Id == id);

    /// <summary>Window <paramref name="window"/> comes to the top (a minimized one shows again), gets
    /// the keyboard, and lights its title bar.</summary>
    internal void Activate(Window window)
    {
        if (!window.Desktop)
        {
            list.Remove(window);
            list.Add(window);
        }
        window.Unminimize();
        if (!ReferenceEquals(Active, window))
        {
            var before = Active;
            Active = window;
            before?.Configure();
            before?.Bar?.Draw();
            window.Configure();
            display.Keyboard.Give(window.Surface);
            window.Tell("focused");
        }
        if (!window.Picture.Empty) window.Bar?.Draw();
        display.Frame.Redraw(window.Outer, default);
    }

    /// <summary>The keyboard goes to whatever is on top now.</summary>
    internal void ActivateTop()
    {
        var top = list.LastOrDefault(w => !w.Desktop && w.Shown != Shown.Minimized) ?? Desktop;
        if (top != null) Activate(top);
    }

    /// <summary>What is at (x, y): the desktop's parts above the windows, then the windows top down.</summary>
    internal IPart? Hit(int x, int y)
    {
        if (Desktop is { } d && (TaskbarRect.Contains(x, y) || d.IsAbove(x, y)))
            return d.Part(x, y);
        for (var i = list.Count - 1; i >= 0; i--)
            if (list[i].Part(x, y) is { } part) return part;
        return null;
    }

    /// <summary>Row <paramref name="y"/> of the windows, bottom to top, then the desktop's parts above
    /// them. The whole line is written: the bottom window fills it (or it is cleared).</summary>
    internal void Draw(int y, int x0, Span<byte> line)
    {
        if (list.Count == 0) line.Clear();
        for (var i = 0; i < list.Count; i++) list[i].Draw(y, x0, line, bottom: i == 0);
        Desktop?.DrawAbove(y, x0, line, TaskbarRect);
    }
}

/// <summary>The menus (popups), in the order they opened: later ones on top.</summary>
internal sealed class Popups
{
    private readonly List<XdgPopup> list = new();

    internal void Add(XdgPopup popup) => list.Add(popup);
    internal bool Remove(XdgPopup popup) => list.Remove(popup);

    internal IPart? Hit(int x, int y)
    {
        for (var i = list.Count - 1; i >= 0; i--)
            if (list[i].Picture.Rect.Contains(x, y))
                return new Content(null, new Target(list[i].Surface, list[i].Picture.Rect.Corner));
        return null;
    }

    internal void Draw(int y, int x0, Span<byte> line)
    {
        foreach (var p in list) p.Picture.Draw(y, x0, line);
    }
}
