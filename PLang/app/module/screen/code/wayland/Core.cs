namespace app.module.screen.code.wayland;

// The core of the Wayland protocol, as Chromium uses it. Each class is one protocol interface,
// named as the protocol names it; it owns its requests (Request) and writes its own events.

/// <summary>wl_display: object 1 of every client.</summary>
internal sealed class WlDisplay(Client client) : Resource(client, 1, 1)
{
    internal override void Request(ushort opcode, Args args)
    {
        switch (opcode)
        {
            case 0: new WlCallback(Client, args.NewId()).Done(Display.Serial()); break;   // sync
            case 1: new WlRegistry(Client, args.NewId()).Announce(); break;                  // get_registry
        }
    }

    /// <summary>An id the client may use again.</summary>
    internal void Deleted(uint id) => Event(1).Uint(id).Send();
}

/// <summary>wl_registry: the globals a client can bind.</summary>
internal sealed class WlRegistry(Client client, uint id) : Resource(client, id, 1)
{
    internal void Announce()
    {
        foreach (var global in Display.Globals)
            Event(0).Uint(global.Name).String(global.Interface).Uint(global.Version).Send();
    }

    internal override void Request(ushort opcode, Args args)
    {
        if (opcode != 0) return;   // bind(name, interface, version, id)
        var name = args.Uint();
        args.String();
        var version = args.Uint();
        var id = args.NewId();
        Display.Globals.FirstOrDefault(g => g.Name == name)?.Bind(Client, id, version);
    }
}

/// <summary>A global: an interface every client can bind, at most at its version.</summary>
internal sealed record Global(uint Name, string Interface, uint Version, Func<Client, uint, uint, Resource> Make)
{
    internal Resource Bind(Client client, uint id, uint version) => Make(client, id, Math.Min(version, Version));
}

/// <summary>wl_callback: answered once, then gone.</summary>
internal sealed class WlCallback(Client client, uint id) : Resource(client, id, 1)
{
    internal override void Request(ushort opcode, Args args) { }

    internal void Done(uint data)
    {
        Event(0).Uint(data).Send();
        Destroy();
    }
}

/// <summary>wl_compositor: makes surfaces and regions.</summary>
internal sealed class WlCompositor(Client client, uint id, uint version) : Resource(client, id, version)
{
    internal override void Request(ushort opcode, Args args)
    {
        switch (opcode)
        {
            case 0: _ = new WlSurface(Client, args.NewId(), Version); break;
            case 1: _ = new WlRegion(Client, args.NewId(), Version); break;
        }
    }
}

/// <summary>wl_region: rectangles (input and opaque regions). PlangOS doesn't need their shape.</summary>
internal sealed class WlRegion(Client client, uint id, uint version) : Resource(client, id, version)
{
    internal override void Request(ushort opcode, Args args)
    {
        if (opcode == 0) Destroy();
    }
}

/// <summary>
/// wl_surface: what a client draws into. Its state is double-buffered: attach, damage and frame
/// requests wait for commit. What it is on the screen — a window, a menu, part of a window — is its
/// <see cref="Role"/>.
/// </summary>
internal sealed class WlSurface(Client client, uint id, uint version) : Resource(client, id, version)
{
    private WlBuffer? attached;
    private bool hasAttached;
    private readonly List<Rect> damage = new();
    private readonly List<WlCallback> callbacks = new();

    /// <summary>What this surface is on the screen: set by xdg_surface.get_toplevel/get_popup or
    /// wl_subcompositor.get_subsurface.</summary>
    internal ISurfaceRole? Role { get; set; }
    internal XdgSurface? Xdg { get; set; }
    /// <summary>The surfaces placed on this one (wl_subsurface).</summary>
    internal List<WlSubsurface> Children { get; } = new();

    internal override void Request(ushort opcode, Args args)
    {
        switch (opcode)
        {
            case 0: Destroy(); break;
            case 1: attached = args.Object<WlBuffer>(); hasAttached = true; break;
            case 2: case 9: damage.Add(new Rect(args.Int(), args.Int(), args.Int(), args.Int())); break;   // damage, damage_buffer
            case 3: callbacks.Add(new WlCallback(Client, args.NewId())); break;
            case 6: Commit(); break;
        }
    }

    /// <summary>What was attached, damaged and asked for becomes current: the role shows it.</summary>
    private void Commit()
    {
        Xdg?.Committed();
        var update = new Update(hasAttached ? attached : null, hasAttached, [.. damage], [.. callbacks]);
        hasAttached = false;
        attached = null;
        damage.Clear();
        callbacks.Clear();
        if (Role != null) Role.Commit(this, update);
        else update.Skip(Display);   // no role (yet): nothing to show — but the client gets its buffer back
    }

    protected override void Destroyed()
    {
        Role?.Gone();
        Role = null;
    }
}

/// <summary>One commit's worth of a surface: the new buffer (if one was attached), where it
/// changed, and the frame callbacks to answer once it is on its way.</summary>
internal sealed record Update(WlBuffer? Buffer, bool Attached, List<Rect> Damage, List<WlCallback> Callbacks)
{
    /// <summary>The buffer's pixels, copied out (the client may reuse it at once) — into
    /// <paramref name="into"/> when it is the same size, so a window's pixels live in one array for
    /// as long as it keeps its size, and then only where it changed — then released.</summary>
    internal Picture? Take(Picture? into = null)
    {
        if (Buffer == null) return null;
        var picture = Buffer.Into(into, Damage);
        Buffer.Release();
        return picture;
    }

    /// <summary>Tells the client its frame is done: it may draw the next.</summary>
    internal void Answer(Display display)
    {
        var time = display.Time();
        foreach (var callback in Callbacks) callback.Done(time);
    }

    /// <summary>Later, on the display's next tick (nothing new was shown).</summary>
    internal void Defer(Display display) => display.Frame.Later(Callbacks);

    /// <summary>Not shown: the buffer goes back at once, the callbacks on the next tick.</summary>
    internal void Skip(Display display)
    {
        Buffer?.Release();
        Defer(display);
    }
}

/// <summary>What a surface is on the screen.</summary>
internal interface ISurfaceRole
{
    void Commit(WlSurface surface, Update update);
    void Gone();
}

/// <summary>wl_shm: shared memory pools.</summary>
internal sealed class WlShm : Resource
{
    internal WlShm(Client client, uint id, uint version) : base(client, id, version)
    {
        Event(0).Uint(0).Send();   // ARGB8888
        Event(0).Uint(1).Send();   // XRGB8888
    }

    internal override void Request(ushort opcode, Args args)
    {
        if (opcode == 0) _ = new WlShmPool(Client, args.NewId(), args.Fd(), args.Int());
    }
}

/// <summary>wl_shm_pool: a client's shared memory, mapped here; its buffers are parts of it. The
/// mapping stays while the pool or any of its buffers does.</summary>
internal sealed class WlShmPool : Resource
{
    private readonly int fd;
    private int size;
    private int holders = 1;   // the pool itself, then each buffer
    internal IntPtr Memory { get; private set; }
    internal int Size => size;

    internal WlShmPool(Client client, uint id, int fd, int size) : base(client, id, 1)
    {
        this.fd = fd;
        this.size = size;
        Memory = Native.Map(fd, size);
        if (Memory == IntPtr.Zero) Display.Debug($"wayland: pool#{id} fd {fd} size {size}: not mapped (errno {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()})");
    }

    internal override void Request(ushort opcode, Args args)
    {
        switch (opcode)
        {
            case 0:
                holders++;
                _ = new WlBuffer(Client, args.NewId(), this, args.Int(), args.Int(), args.Int(), args.Int(), args.Uint());
                break;
            case 1: Destroy(); break;
            case 2:
                var grown = args.Int();
                Native.Unmap(Memory, size);
                size = grown;
                Memory = Native.Map(fd, size);
                break;
        }
    }

    internal void Let()
    {
        if (--holders > 0) return;
        Native.Unmap(Memory, size);
        Memory = IntPtr.Zero;
        Native.close(fd);
    }

    protected override void Destroyed() => Let();
}

/// <summary>wl_buffer: a picture in a pool.</summary>
internal sealed class WlBuffer(Client client, uint id, WlShmPool pool, int offset, int width, int height, int stride, uint format)
    : Resource(client, id, 1)
{
    internal override void Request(ushort opcode, Args args)
    {
        if (opcode == 0) Destroy();
    }

    /// <summary>Its pixels, out of shared memory. Into <paramref name="into"/> if it is a picture of
    /// the same size and kind — it holds the pixels as they were, so only what changed
    /// (<paramref name="damage"/>) is copied — else all of them into a new one (pinned: it lives long
    /// and never moves).</summary>
    internal Picture Into(Picture? into, IReadOnlyList<Rect> damage)
    {
        var opaque = format != 0;
        var all = new Rect(0, 0, width, height);
        var kept = into is { Empty: false } p && p.Rect.Width == width && p.Rect.Height == height && p.Opaque == opaque;
        var picture = kept ? into! : new Picture(all, GC.AllocateUninitializedArray<byte>(width * height * 4, pinned: true), opaque);
        if (pool.Memory == IntPtr.Zero || offset + (long)stride * (height - 1) + width * 4 > pool.Size) return picture;
        if (!kept || damage.Count is 0 or > 16) Copy(all, picture);
        else foreach (var changed in damage) Copy(changed.Clip(all), picture);
        return picture;
    }

    private void Copy(Rect part, Picture picture)
    {
        if (part.Empty) return;
        var bytes = part.Width * 4;
        for (var y = part.Y; y < part.Bottom; y++)
            System.Runtime.InteropServices.Marshal.Copy(pool.Memory + offset + y * stride + part.X * 4, picture.Pixels, (y * width + part.X) * 4, bytes);
    }

    internal void Release() => Event(0).Send();

    protected override void Destroyed() => pool.Let();
}

/// <summary>wl_subcompositor: surfaces placed on other surfaces.</summary>
internal sealed class WlSubcompositor(Client client, uint id, uint version) : Resource(client, id, version)
{
    internal override void Request(ushort opcode, Args args)
    {
        switch (opcode)
        {
            case 0: Destroy(); break;
            case 1:
                var id = args.NewId();
                if (args.Object<WlSurface>() is { } surface && args.Object<WlSurface>() is { } parent)
                    _ = new WlSubsurface(Client, id, surface, parent);
                break;
        }
    }
}

/// <summary>wl_subsurface: a surface drawn on its parent, at a position from the parent's corner.</summary>
internal sealed class WlSubsurface : Resource, ISurfaceRole
{
    private readonly WlSurface surface;
    private readonly WlSurface parent;
    internal Point At { get; private set; }
    internal Picture? Picture { get; private set; }

    internal WlSubsurface(Client client, uint id, WlSurface surface, WlSurface parent) : base(client, id, 1)
    {
        this.surface = surface;
        this.parent = parent;
        surface.Role = this;
        parent.Children.Add(this);
    }

    internal override void Request(ushort opcode, Args args)
    {
        switch (opcode)
        {
            case 0: Destroy(); break;
            case 1: At = new Point(args.Int(), args.Int()); break;
        }
    }

    public void Commit(WlSurface _, Update update)
    {
        if (update.Attached) Picture = update.Take(Picture);
        Display.Windows.Of(parent)?.Changed();
        update.Answer(Display);
    }

    public void Gone()
    {
        parent.Children.Remove(this);
        Display.Windows.Of(parent)?.Changed();
    }

    protected override void Destroyed() => Gone();
}

/// <summary>wl_output: the one screen.</summary>
internal sealed class WlOutput : Resource
{
    internal WlOutput(Client client, uint id, uint version) : base(client, id, version)
    {
        var size = Display.Size;
        Event(0).Int(0).Int(0).Int(0).Int(0).Int(0).String("PlangOS").String("screen").Int(0).Send();
        Event(1).Uint(3).Int(size.Width).Int(size.Height).Int(60000).Send();   // current | preferred, 60 Hz
        if (Version >= 2) Event(3).Int(1).Send();
        if (Version >= 4) { Event(4).String("plang-screen").Send(); Event(5).String("PlangOS screen").Send(); }
        Done();
    }

    internal void Done()
    {
        if (Version >= 2) Event(2).Send();
    }

    internal override void Request(ushort opcode, Args args)
    {
        if (opcode == 0) Destroy();
    }
}

/// <summary>zxdg_output_manager_v1: the screen's logical size.</summary>
internal sealed class XdgOutputManager(Client client, uint id, uint version) : Resource(client, id, version)
{
    internal override void Request(ushort opcode, Args args)
    {
        switch (opcode)
        {
            case 0: Destroy(); break;
            case 1:
                var id = args.NewId();
                _ = new XdgOutput(Client, id, Version, args.Object<WlOutput>());
                break;
        }
    }
}

internal sealed class XdgOutput : Resource
{
    internal XdgOutput(Client client, uint id, uint version, WlOutput? output) : base(client, id, version)
    {
        var size = Display.Size;
        Event(0).Int(0).Int(0).Send();
        Event(1).Int(size.Width).Int(size.Height).Send();
        if (Version >= 2) { Event(3).String("plang-screen").Send(); Event(4).String("PlangOS screen").Send(); }
        if (Version < 3) Event(2).Send();
        output?.Done();
    }

    internal override void Request(ushort opcode, Args args)
    {
        if (opcode == 0) Destroy();
    }
}
