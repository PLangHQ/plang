using System.Text.Json.Nodes;

namespace app.module.screen.code.wayland;

internal enum Shown { Normal, Maximized, Minimized }

/// <summary>
/// A window on the screen: a client's toplevel, with the title bar plang-screen draws above it.
/// It places itself, draws itself, says what of it is under the pointer, and answers what its
/// client and its title bar ask (move, resize, maximize …). The first window is the desktop: the
/// whole screen, no title bar, under every other window — except the parts of it that are above
/// them (its taskbar, and whatever it asks for: its start menu).
/// </summary>
internal sealed class Window : ISurfaceRole
{
    /// <summary>How far outside a window its edges can be grabbed.</summary>
    internal const int Band = 8;
    /// <summary>How far along an edge from a corner the grab is that corner's (both its edges).</summary>
    internal const int Reach = 24;
    private static readonly (int w, int h) Smallest = (240, 160);

    private Point offset;                 // where the window starts inside its buffer (shadows around it)
    private Shown was = Shown.Normal;     // how it showed before it was minimized
    private Rect restore;                 // where it was before it was maximized
    private Rect onScreen;                // where it was last drawn (At changes before the client redraws at its new size)

    /// <summary>All it draws: the window with its title bar, its picture — which, while it is resized,
    /// can reach past the size the client says it is — and the surfaces placed on it.</summary>
    private Rect Drawn
    {
        get
        {
            var all = Outer.Merge(Picture.Rect);
            foreach (var child in Surface.Children)
                if (child.Picture is { } p)
                    all = all.Merge(Rect.At(Picture.Rect.Corner + child.At, p.Rect.Width, p.Rect.Height));
            return all;
        }
    }
    private Size asked;                   // the size last asked of the client
    private bool resizing;
    private string sent = "";             // the last configure, so an unchanged one isn't sent again

    internal Display Display { get; }
    internal XdgToplevel Toplevel { get; }
    internal int Id { get; }
    internal bool Desktop { get; }
    internal TitleBar? Bar { get; }
    /// <summary>The size grip in the bottom-right corner (not the desktop's).</summary>
    internal SizeGrip? SizeGrip { get; }
    internal Point At { get; private set; }            // the page's top-left on the screen (the title bar is above it)
    internal Size Size { get; private set; }           // the page's size
    internal Picture Picture { get; private set; } = Picture.None;
    internal Shown Shown { get; private set; } = Shown.Normal;
    internal Address Address { get; set; } = Address.None;   // where it is: what the globe shows, and where that really is
    internal IReadOnlyList<string> Tools { get; private set; } = [];   // the app's own, in the title bar (in place of back, forward)
    internal Rect? Above { get; private set; }         // the desktop's part above the windows (its start menu)

    internal Window(Display display, XdgToplevel toplevel, int id, bool desktop, Point at, Size size)
    {
        Display = display;
        Toplevel = toplevel;
        Id = id;
        Desktop = desktop;
        At = at;
        Size = asked = size;
        restore = Rect.At(at, size.Width, size.Height);
        if (!desktop)
        {
            Bar = new TitleBar(this);
            SizeGrip = new SizeGrip(this);
        }
    }

    internal WlSurface Surface => Toplevel.Xdg.Surface;
    internal bool Active => ReferenceEquals(Display.Windows.Active, this);
    internal bool Visible => Shown != Shown.Minimized && !Picture.Empty;

    /// <summary>The page on the screen.</summary>
    internal Rect Frame => Rect.At(At, Size.Width, Size.Height);

    /// <summary>The window with its title bar.</summary>
    internal Rect Outer => Desktop ? Picture.Rect : new Rect(At.X, At.Y - TitleBar.Height, Size.Width, Size.Height + TitleBar.Height);

    // ---- what the client shows ----------------------------------------------------------------

    public void Commit(WlSurface surface, Update update)
    {
        if (update.Buffer == null)
        {
            update.Defer(Display);
            return;
        }
        // where it was drawn, not Outer: a maximize or restore has moved At already, while Size is
        // still the old one until this commit
        var old = Visible ? onScreen : default;
        var first = Picture.Empty;
        var picture = update.Take(Picture)!;   // into the same pixels while the size holds
        var geometry = Toplevel.Xdg.Geometry ?? new Rect(0, 0, picture.Rect.Width, picture.Rect.Height);
        var resized = Size != new Size(geometry.Width, geometry.Height);
        offset = geometry.Corner;
        Size = new Size(geometry.Width, geometry.Height);
        Picture = picture;
        Display.Grab?.Committed(this);   // resizing from the left or top: the opposite edge stays
        Place();
        if (first || resized) Bar?.Draw();
        Display.Panel?.Follow(this);
        if (Visible)
        {
            if (first || old != Drawn) Display.Frame.Redraw(old, Drawn);
            else
            {
                var corner = Picture.Rect.Corner;
                if (update.Damage.Count is 0 or > 16) Display.Frame.Present(Picture.Rect);
                else foreach (var d in update.Damage) Display.Frame.Present(d.Moved(corner));
                Display.Frame.Send();
            }
            onScreen = Drawn;
        }
        update.Answer(Display);
    }

    /// <summary>A surface placed on this window's (a subsurface) changed.</summary>
    internal void Changed()
    {
        if (!Visible) return;
        // where it was and where it is: a surface on it may have moved or shrunk
        Display.Frame.Redraw(onScreen, Drawn);
        onScreen = Drawn;
    }

    public void Gone() => Display.Windows.Close(this);

    internal void Titled()
    {
        if (Desktop) return;
        Tell("titled", new JsonObject { ["title"] = Toplevel.Title });
        if (!Picture.Empty) Bar?.Draw();
    }

    // ---- where it is and what it looks like -----------------------------------------------------

    private void Place()
    {
        Picture.Place(At - offset);
        Bar?.Place();
        SizeGrip?.Place();
    }

    /// <summary>Row <paramref name="y"/> of this window over <paramref name="line"/>; as the bottom
    /// one (<paramref name="bottom"/>: the desktop), it fills the whole line — nothing under it.</summary>
    internal void Draw(int y, int x0, Span<byte> line, bool bottom = false)
    {
        if (!Visible)
        {
            if (bottom) line.Clear();
            return;
        }
        if (bottom) Picture.Base(y, x0, line);
        Bar?.Picture.Draw(y, x0, line);
        if (!bottom) Picture.Draw(y, x0, line);
        foreach (var child in Surface.Children)
            if (child.Picture is { } p)
            {
                p.Place(Picture.Rect.Corner + child.At);
                p.Draw(y, x0, line);
            }
        SizeGrip?.Draw(y, x0, line);   // over the page's corner
    }

    /// <summary>The desktop's parts above the windows — its taskbar (<paramref name="taskbar"/>) and
    /// what it asked for — drawn again, over them.</summary>
    internal void DrawAbove(int y, int x0, Span<byte> line, Rect taskbar)
    {
        DrawPart(y, x0, line, taskbar);
        if (Above is { } above) DrawPart(y, x0, line, above);
    }

    private void DrawPart(int y, int x0, Span<byte> line, Rect part)
    {
        if (part.Empty || y < part.Y || y >= part.Bottom) return;
        int from = Math.Max(x0, part.X), to = Math.Min(x0 + line.Length / 4, part.Right);
        if (to > from) Picture.Draw(y, from, line[((from - x0) * 4)..((to - x0) * 4)]);
    }

    internal bool IsAbove(int x, int y) => Above is { } a && a.Contains(x, y);

    /// <summary>What of this window is at (x, y): its page, its size grip, a part of its title bar, or an
    /// edge. Near a corner (within <see cref="Reach"/> of it along an edge) an edge is that corner's
    /// — both its edges — so a corner is easy to catch.</summary>
    internal IPart? Part(int x, int y)
    {
        if (!Visible) return null;
        var page = new Content(this, new Target(Surface, Picture.Rect.Corner));
        if (Desktop) return Picture.Rect.Contains(x, y) ? page : null;
        if (SizeGrip?.Holds(x, y) == true) return new Edge(this, 2 | 8);
        if (Frame.Contains(x, y)) return page;
        if (Outer.Contains(x, y)) return Bar!.Hit(x - At.X);
        if (Shown == Shown.Maximized || !Outer.Grown(Band).Contains(x, y)) return null;
        bool left = x < Outer.X, right = x >= Outer.Right, top = y < Outer.Y, bottom = y >= Outer.Bottom;
        if (left || right)
        {
            top |= y < Outer.Y + Reach;
            bottom |= y >= Outer.Bottom - Reach;
        }
        if (top || bottom)
        {
            left |= x < Outer.X + Reach;
            right |= x >= Outer.Right - Reach;
        }
        return new Edge(this, (top ? 1u : 0) | (bottom ? 2u : 0) | (left ? 4u : 0) | (right ? 8u : 0));
    }

    // ---- what it is asked -------------------------------------------------------------------

    /// <summary>Tells the client its size and states — only when they changed, unless forced (the first configure).</summary>
    internal void Configure(bool force = false)
    {
        var states = new List<uint>();
        // the desktop is maximized to the whole screen, not fullscreen: fullscreen, Chromium shows
        // its "press Esc to exit full screen" bubble
        if (Desktop || Shown == Shown.Maximized) states.Add(1);       // maximized
        if (resizing) states.Add(3);                                  // resizing
        if (Active || Desktop) states.Add(4);                         // activated
        var now = $"{asked.Width}x{asked.Height}:{string.Join(',', states)}";
        if (!Toplevel.Xdg.Configured || (!force && now == sent)) return;
        sent = now;
        Toplevel.Configure(asked, states);
    }

    private void Ask(Size size)
    {
        asked = size;
        Configure();
    }

    internal void Activate() => Display.Windows.Activate(this);

    internal void Toggle()
    {
        if (Shown == Shown.Maximized) Restore(); else Maximize();
    }

    internal void Maximize()
    {
        if (Desktop || Shown == Shown.Maximized) return;
        restore = Frame;
        Shown = Shown.Maximized;
        var work = Display.Windows.Work;
        At = new Point(work.X, work.Y + TitleBar.Height);
        Ask(new Size(work.Width, work.Height - TitleBar.Height));
        Tell("maximized");
    }

    internal void Restore()
    {
        if (Shown == Shown.Minimized) { Activate(); return; }
        if (Shown != Shown.Maximized) return;
        Shown = Shown.Normal;
        At = restore.Corner;
        Ask(new Size(restore.Width, restore.Height));
        Tell("restored");
    }

    internal void Minimize()
    {
        if (Desktop || Shown == Shown.Minimized) return;
        was = Shown;
        Shown = Shown.Minimized;
        Tell("minimized");
        Display.Panel?.Closed(this);
        Display.Frame.Redraw(Outer, default);
        if (Active) Display.Windows.ActivateTop();
    }

    /// <summary>Shows again what was minimized (as it was: normal or maximized).</summary>
    internal bool Unminimize()
    {
        if (Shown != Shown.Minimized) return false;
        Shown = was;
        Tell("restored");
        return true;
    }

    /// <summary>Its top-left page corner goes to <paramref name="to"/> (kept on the screen, under the taskbar).</summary>
    internal void MoveTo(Point to)
    {
        var old = Drawn;
        var work = Display.Windows.Work;
        At = to with { Y = Math.Clamp(to.Y, TitleBar.Height, work.Bottom - 8) };
        Place();
        var now = Drawn;
        if (now == old) return;
        // the host moves the pixels it has; only what the window uncovered is sent, and whatever
        // lies over its new place (the taskbar, a menu) is put right
        Display.Frame.Move(old, now.Corner - old.Corner);
        foreach (var uncovered in old.Without(now)) Display.Frame.Present(uncovered);
        Display.Frame.Present(now);
        onScreen = now;
        Display.Panel?.Follow(this);
        Display.Frame.Send();
    }

    /// <summary>A resize asks the client for the size; the window takes it when the client draws it.</summary>
    internal void ResizeTo(Size size, bool going)
    {
        resizing = going;
        Ask(new Size(Math.Max(Smallest.w, size.Width), Math.Max(Smallest.h, size.Height)));
    }

    /// <summary>During a resize from the left or top: the opposite edge of <paramref name="from"/> stays.</summary>
    internal void Anchor(Rect from, uint edges)
    {
        if ((edges & 4) != 0) At = At with { X = from.Right - Size.Width };
        if ((edges & 1) != 0) At = At with { Y = from.Bottom - Size.Height };
    }

    /// <summary>The client asks to be moved (it draws its own frame).</summary>
    internal void Move()
    {
        if (!Desktop && Shown == Shown.Normal) Display.Hold(new MoveGrab(this, Display.Pointer.Pressed));
    }

    internal void Resize(uint edges)
    {
        if (!Desktop && Shown == Shown.Normal) Display.Hold(new ResizeGrab(this, edges, Display.Pointer.Pressed));
    }

    /// <summary>Keys to this window: <paramref name="key"/> with <paramref name="mods"/> held (evdev codes).</summary>
    internal void Press(uint key, params uint[] mods)
    {
        Activate();
        Display.Keyboard.Press(key, mods);
    }

    /// <summary>A command from PLang (the taskbar): focus, minimize, maximize, restore, close; the
    /// page it shows (url); the app's tools in its title bar (tools); the clipboard as a value, for its
    /// page (paste); the desktop's part above the windows (above).</summary>
    internal void Command(string what, JsonObject e)
    {
        int N(string k) => e[k] is JsonValue v && v.TryGetValue<double>(out var d) ? (int)d : 0;
        switch (what)
        {
            case "focus": Activate(); break;
            case "minimize": Minimize(); break;
            case "maximize": Maximize(); break;
            case "restore": Restore(); break;
            case "close": Toplevel.Close(); break;
            case "url": Address = Address.Of(e["url"]); break;
            case "paste": Display.Tell(new JsonObject { ["paste"] = Display.Clipboard.Value, ["id"] = Id }); break;   // the clipboard as a value, to its page
            case "tools":
                Tools = e["tools"] is JsonArray names
                    ? names.Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).OfType<string>().Take(8).ToList()
                    : [];
                Bar?.Arrange();
                if (!Picture.Empty) Bar?.Draw();
                break;
            case "above" when Desktop:
                var old = Above ?? default;
                var now = new Rect(N("x"), N("y"), N("w"), N("h"));
                Above = now.Empty ? null : now;
                Display.Frame.Redraw(old, now);
                break;
        }
    }

    /// <summary>The click landed elsewhere: the desktop's start menu part goes back under the windows.</summary>
    internal void Lower()
    {
        if (Above is not { } a) return;
        Above = null;
        Display.Frame.Redraw(a, default);
    }

    /// <summary>What happened to this window, for PLang (the taskbar).</summary>
    internal void Tell(string what, JsonObject? more = null)
    {
        if (Desktop) return;
        var e = more ?? new JsonObject();
        e["window"] = what;
        e["id"] = Id;
        Display.Tell(e);
    }

    /// <summary>Asks PLang to send this window somewhere (the address field, the menu's pages).</summary>
    internal void Navigate(string where) => Display.Tell(new JsonObject { ["navigate"] = where, ["id"] = Id });

    /// <summary>One of the app's tools was clicked: PLang hands it to the page.</summary>
    internal void Tool(string name) => Display.Tell(new JsonObject { ["tool"] = name, ["id"] = Id });

    /// <summary>Asks PLang to open a window of its own (the menu's New window).</summary>
    internal void OpenAnother() => Display.Tell(new JsonObject { ["open"] = Address.Source, ["id"] = Id });
}
