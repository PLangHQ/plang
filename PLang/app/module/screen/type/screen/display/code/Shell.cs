namespace app.module.screen.type.screen.display.code;

// xdg-shell (windows and menus) and xdg-decoration (who draws the frame). The protocol objects
// speak; a toplevel's Window and a popup's placement are the display's.

/// <summary>xdg_wm_base: makes xdg surfaces and positioners.</summary>
internal sealed class XdgWmBase(Client client, uint id, uint version) : Resource(client, id, version)
{
    internal override void Request(ushort opcode, Args args)
    {
        switch (opcode)
        {
            case 0: Destroy(); break;
            case 1: _ = new XdgPositioner(Client, args.NewId(), Version); break;
            case 2:
                var id = args.NewId();
                if (args.Object<WlSurface>() is { } surface) _ = new XdgSurface(Client, id, Version, surface);
                break;
        }
    }
}

/// <summary>xdg_positioner: where a menu goes, relative to its parent's window geometry.</summary>
internal sealed class XdgPositioner(Client client, uint id, uint version) : Resource(client, id, version)
{
    private int width, height, offsetX, offsetY;
    private Rect anchorRect;
    private uint anchor, gravity;
    internal uint Constraints { get; private set; }

    internal override void Request(ushort opcode, Args args)
    {
        switch (opcode)
        {
            case 0: Destroy(); break;
            case 1: width = args.Int(); height = args.Int(); break;
            case 2: anchorRect = new Rect(args.Int(), args.Int(), args.Int(), args.Int()); break;
            case 3: anchor = args.Uint(); break;
            case 4: gravity = args.Uint(); break;
            case 5: Constraints = args.Uint(); break;
            case 6: offsetX = args.Int(); offsetY = args.Int(); break;
        }
    }

    /// <summary>The menu's rectangle: the anchor's point on the anchor rectangle, the menu on the
    /// gravity's side of it, moved by the offset. (anchor/gravity: 0 none, 1 top, 2 bottom, 3 left,
    /// 4 right, 5 top-left, 6 bottom-left, 7 top-right, 8 bottom-right)</summary>
    internal Rect Placement()
    {
        static bool Left(uint e) => e is 3 or 5 or 6;
        static bool Right(uint e) => e is 4 or 7 or 8;
        static bool Top(uint e) => e is 1 or 5 or 7;
        static bool Bottom(uint e) => e is 2 or 6 or 8;
        var ax = Left(anchor) ? anchorRect.X : Right(anchor) ? anchorRect.Right : anchorRect.X + anchorRect.Width / 2;
        var ay = Top(anchor) ? anchorRect.Y : Bottom(anchor) ? anchorRect.Bottom : anchorRect.Y + anchorRect.Height / 2;
        var x = Left(gravity) ? ax - width : Right(gravity) ? ax : ax - width / 2;
        var y = Top(gravity) ? ay - height : Bottom(gravity) ? ay : ay - height / 2;
        return new Rect(x + offsetX, y + offsetY, width, height);
    }
}

/// <summary>xdg_surface: a surface that becomes a window (toplevel) or a menu (popup). Its window
/// geometry — the part that is the window, inside the buffer's shadows — is double-buffered.</summary>
internal sealed class XdgSurface : Resource
{
    private Rect? pendingGeometry;
    internal WlSurface Surface { get; }
    internal Rect? Geometry { get; private set; }
    internal bool Configured { get; private set; }
    internal XdgToplevel? Toplevel { get; private set; }
    internal XdgPopup? Popup { get; private set; }

    internal XdgSurface(Client client, uint id, uint version, WlSurface surface) : base(client, id, version)
    {
        Surface = surface;
        surface.Xdg = this;
    }

    internal override void Request(ushort opcode, Args args)
    {
        switch (opcode)
        {
            case 0: Destroy(); break;
            case 1: Toplevel = new XdgToplevel(Client, args.NewId(), Version, this); break;
            case 2:
                var id = args.NewId();
                var parent = args.Object<XdgSurface>();
                if (args.Object<XdgPositioner>() is { } positioner) Popup = new XdgPopup(Client, id, Version, this, parent, positioner);
                break;
            case 3: pendingGeometry = new Rect(args.Int(), args.Int(), args.Int(), args.Int()); break;
        }
    }

    /// <summary>The surface committed: its new geometry holds; the first commit is answered with the
    /// first configure.</summary>
    internal void Committed()
    {
        if (pendingGeometry != null) Geometry = pendingGeometry;
        pendingGeometry = null;
        if (Configured) return;
        Configured = true;
        if (Toplevel?.Window is { } window) window.Configure(force: true);
        else Popup?.Configure();
    }

    internal void Configure() => Event(0).Uint(Display.Serial()).Send();
}

/// <summary>xdg_toplevel: a window. What it asks (title, move, maximize …) goes to its
/// <see cref="Window"/>; the window answers with configure.</summary>
internal sealed class XdgToplevel : Resource
{
    internal XdgSurface Xdg { get; }
    internal Window Window { get; }
    internal XdgDecoration? Decoration { get; set; }
    internal string Title { get; private set; } = "";
    internal string App { get; private set; } = "";

    internal XdgToplevel(Client client, uint id, uint version, XdgSurface xdg) : base(client, id, version)
    {
        Xdg = xdg;
        Window = Display.Windows.Open(this);
        xdg.Surface.Role = Window;
    }

    internal override void Request(ushort opcode, Args args)
    {
        switch (opcode)
        {
            case 0: Destroy(); break;
            case 2: Title = args.String() ?? ""; Window.Titled(); break;
            case 3: App = args.String() ?? ""; break;
            case 5: Window.Move(); break;
            case 6: args.Object<WlSeat>(); args.Uint(); Window.Resize(args.Uint()); break;
            case 9: Window.Maximize(); break;
            case 10: Window.Restore(); break;
            case 11: Window.Maximize(); break;   // fullscreen: the desktop is already; a window fills the work area
            case 12: Window.Configure(force: true); break;
            case 13: Window.Minimize(); break;
        }
    }

    protected override void Destroyed() => Window.Gone();

    /// <summary>configure: size (0: the client chooses) and states — then the surface's configure.</summary>
    internal void Configure(Size size, IEnumerable<uint> states)
    {
        Decoration?.Configure();
        Event(0).Int(size.Width).Int(size.Height).Array(states.SelectMany(BitConverter.GetBytes).ToArray()).Send();
        Xdg.Configure();
    }

    internal void Close() => Event(1).Send();
}

/// <summary>A window's size (content, without its title bar).</summary>
internal readonly record struct Size(int Width, int Height);

/// <summary>xdg_popup: a menu (a select's list, a context menu, a tooltip): drawn above windows,
/// placed by its positioner relative to its parent.</summary>
internal sealed class XdgPopup : Resource, ISurfaceRole
{
    private readonly XdgSurface xdg;
    private readonly XdgSurface? parent;
    private XdgPositioner positioner;
    private Rect placed;   // relative to the parent's window geometry
    internal Picture Picture { get; private set; } = Picture.None;

    internal XdgPopup(Client client, uint id, uint version, XdgSurface xdg, XdgSurface? parent, XdgPositioner positioner) : base(client, id, version)
    {
        this.xdg = xdg;
        this.parent = parent;
        this.positioner = positioner;
        xdg.Surface.Role = this;
        Display.Popups.Add(this);
        placed = Fitted(positioner.Placement());
    }

    internal WlSurface Surface => xdg.Surface;

    /// <summary>The parent's window geometry corner on the screen.</summary>
    private Point ParentCorner => parent?.Toplevel?.Window.At ?? parent?.Popup?.Corner ?? default;

    /// <summary>Its window geometry corner on the screen.</summary>
    internal Point Corner => ParentCorner + placed.Corner;

    /// <summary>A menu that would leave the screen slides back onto it (when its positioner allows).</summary>
    private Rect Fitted(Rect r)
    {
        var screen = Display.Size;
        var at = ParentCorner + r.Corner;
        var x = (positioner.Constraints & 1) != 0 ? Math.Clamp(at.X, 0, Math.Max(0, screen.Width - r.Width)) : at.X;
        var y = (positioner.Constraints & 2) != 0 ? Math.Clamp(at.Y, 0, Math.Max(0, screen.Height - r.Height)) : at.Y;
        return r with { X = x - ParentCorner.X, Y = y - ParentCorner.Y };
    }

    internal override void Request(ushort opcode, Args args)
    {
        switch (opcode)
        {
            case 0: Destroy(); break;
            case 2:
                if (args.Object<XdgPositioner>() is { } p)
                {
                    positioner = p;
                    placed = Fitted(p.Placement());
                    Event(2).Uint(args.Uint()).Send();   // repositioned
                    Configure();
                }
                break;
        }
    }

    internal void Configure()
    {
        Event(0).Int(placed.X).Int(placed.Y).Int(placed.Width).Int(placed.Height).Send();
        xdg.Configure();
    }

    public void Commit(WlSurface surface, Update update)
    {
        var old = Picture.Rect;
        if (update.Take(Picture) is { } picture)
        {
            var offset = xdg.Geometry?.Corner ?? default;
            picture.Place(Corner - offset);
            Picture = picture;
            Display.Frame.Redraw(old, Picture.Rect);
        }
        update.Answer(Display);
    }

    public void Gone()
    {
        if (!Display.Popups.Remove(this)) return;
        Display.Frame.Redraw(Picture.Rect, default);
    }

    protected override void Destroyed() => Gone();
}

/// <summary>zxdg_decoration_manager_v1: who draws a window's frame.</summary>
internal sealed class XdgDecorationManager(Client client, uint id, uint version) : Resource(client, id, version)
{
    internal override void Request(ushort opcode, Args args)
    {
        switch (opcode)
        {
            case 0: Destroy(); break;
            case 1:
                var id = args.NewId();
                if (args.Object<XdgToplevel>() is { } toplevel) toplevel.Decoration = new XdgDecoration(Client, id, toplevel);
                break;
        }
    }
}

/// <summary>zxdg_toplevel_decoration_v1: always server side — plang-screen draws every title bar.</summary>
internal sealed class XdgDecoration(Client client, uint id, XdgToplevel toplevel) : Resource(client, id, 1)
{
    internal override void Request(ushort opcode, Args args)
    {
        switch (opcode)
        {
            case 0: Destroy(); break;
            case 1: case 2:   // set_mode, unset_mode: the answer is the same
                if (opcode == 1) args.Uint();
                if (toplevel.Xdg.Configured) toplevel.Window.Configure(force: true);
                break;
        }
    }

    internal void Configure() => Event(0).Uint(2).Send();   // server side
}
