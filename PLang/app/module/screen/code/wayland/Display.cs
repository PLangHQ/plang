using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace app.module.screen.code.wayland;

/// <summary>
/// PlangOS's display: a Wayland compositor inside plang. Clients (Chromium) connect to its socket
/// and draw into it; it composes their windows — with the title bars it draws — into the frames the
/// host shows, and takes the host's mouse and keyboard. The root: everything is reached from here,
/// and everything that touches its state does so under <see cref="Gate"/>, one thing at a time.
/// </summary>
internal sealed class Display
{
    private readonly string socketPath;
    private readonly Socket listener = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    private readonly List<Client> clients = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Channel<JsonObject> told = System.Threading.Channels.Channel.CreateUnbounded<JsonObject>();
    private readonly Action<string> debug;
    private volatile bool running = true;
    private IPart? hovered;
    private uint serial;

    internal object Gate { get; } = new();
    internal Size Size { get; }
    internal Font? Font { get; }
    internal IReadOnlyList<Global> Globals { get; }
    internal Windows Windows { get; }
    internal Popups Popups { get; } = new();
    internal Pointer Pointer { get; }
    internal Keyboard Keyboard { get; }
    internal Clipboard Clipboard { get; }
    internal Frame Frame { get; }
    /// <summary>The address field or window menu, when one is open.</summary>
    internal Panel? Panel { get; set; }
    /// <summary>A window being moved or resized.</summary>
    internal Grab? Grab { get; private set; }
    /// <summary>The title bar button the mouse button went down on.</summary>
    internal Button? Pressed { get; set; }

    /// <summary>What happens to windows ({"window":"opened",…}, {"navigate":…}, {"open":…}), for PLang.</summary>
    internal event Func<JsonObject, Task>? Told;

    internal Display(Size size, string layout, string socketPath, Stream? output, byte[]? font, Action<string> debug)
    {
        Size = size;
        this.socketPath = socketPath;
        this.debug = debug;
        Font = Font.From(font);
        Windows = new Windows(this);
        Pointer = new Pointer(this);
        Keyboard = new Keyboard(this, layout);
        Clipboard = new Clipboard(this);
        Frame = new Frame(this, output);
        uint name = 1;
        Globals =
        [
            new(name++, "wl_compositor", 4, (c, id, v) => new WlCompositor(c, id, v)),
            new(name++, "wl_subcompositor", 1, (c, id, v) => new WlSubcompositor(c, id, v)),
            new(name++, "wl_shm", 1, (c, id, v) => new WlShm(c, id, v)),
            new(name++, "xdg_wm_base", 3, (c, id, v) => new XdgWmBase(c, id, v)),
            new(name++, "zxdg_decoration_manager_v1", 1, (c, id, v) => new XdgDecorationManager(c, id, v)),
            new(name++, "wl_seat", 8, (c, id, v) => new WlSeat(c, id, v)),
            new(name++, "zxdg_output_manager_v1", 3, (c, id, v) => new XdgOutputManager(c, id, v)),
            new(name++, "wp_cursor_shape_manager_v1", 1, (c, id, v) => new CursorShapeManager(c, id, v)),
            new(name++, "wl_data_device_manager", 3, (c, id, v) => new WlDataDeviceManager(c, id, v)),
            new(name, "wl_output", 4, (c, id, v) => new WlOutput(c, id, v)),
        ];
    }

    /// <summary>Opens the socket; clients may connect. Frames without news are answered 60 times a second.</summary>
    internal void Start()
    {
        listener.Bind(new UnixDomainSocketEndPoint(socketPath));
        listener.Listen(16);
        new Thread(Accept) { IsBackground = true, Name = "wayland accept" }.Start();
        // a thread of its own: a pool thread waiting on the gate would starve the pool. On a steady
        // 60 Hz beat — each tick at its time, not 16 ms after the last one's work — so a dragged
        // window moves in even steps.
        new Thread(() =>
        {
            var beat = Stopwatch.StartNew();
            long ticks = 0;
            while (running)
            {
                var next = ++ticks * 1000.0 / 60;
                var wait = next - beat.Elapsed.TotalMilliseconds;
                if (wait > 0) Thread.Sleep(TimeSpan.FromMilliseconds(wait));
                else if (wait < -100) ticks = (long)(beat.Elapsed.TotalMilliseconds * 60 / 1000);   // fell far behind: start the beat anew
                lock (Gate)
                {
                    Grab?.Tick();
                    Frame.Tick();
                    Flush();
                }
            }
        }) { IsBackground = true, Name = "wayland tick" }.Start();
        _ = Task.Run(Pump);
    }

    private void Accept()
    {
        while (true)
        {
            Socket socket;
            try { socket = listener.Accept(); }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException) { return; }
            Client client;
            lock (Gate)
            {
                client = new Client(this, socket);
                clients.Add(client);
                Debug("screen: a client connected");
            }
            new Thread(client.Run) { IsBackground = true, Name = "wayland client" }.Start();
        }
    }

    /// <summary>What happened goes to PLang in order, outside the gate: a goal may call back in.</summary>
    private async Task Pump()
    {
        await foreach (var e in told.Reader.ReadAllAsync())
            foreach (var handler in Told?.GetInvocationList().Cast<Func<JsonObject, Task>>() ?? [])
            {
                try { await handler(e); }
                catch (Exception ex) { Debug("screen: " + ex.Message); }
            }
    }

    internal void Tell(JsonObject e) => told.Writer.TryWrite(e);
    internal void Debug(string message) => debug(message);
    internal uint Serial() => ++serial;
    internal uint Time() => unchecked((uint)clock.ElapsedMilliseconds);
    internal void Gone(Client client) => clients.Remove(client);

    /// <summary>What each client is owed, sent.</summary>
    internal void Flush()
    {
        foreach (var client in clients.ToList()) client.Flush();
    }

    /// <summary>Row <paramref name="y"/> of the screen, bottom to top: windows (with the desktop's
    /// parts above them), menus, the open panel.</summary>
    internal void Draw(int y, int x0, Span<byte> line)
    {
        Windows.Draw(y, x0, line);
        Popups.Draw(y, x0, line);
        Panel?.Picture.Draw(y, x0, line);
    }

    /// <summary>The pointer holds a window; every surface lets go of the pointer.</summary>
    internal void Hold(Grab grab)
    {
        Grab = grab;
        Pointer.Leave();
    }

    /// <summary>A panel opens (the one before closes).</summary>
    internal void Open(Panel panel)
    {
        Panel?.Close();
        Panel = panel;
        panel.Draw();
    }

    // ---- input from the host: one JSON line each ------------------------------------------------

    /// <summary>{"mouse":…}, {"key":…}, {"text":…}, {"clipboard":…} from the host; {"window":…} from
    /// PLang (the taskbar). "t" is a stamp to echo.</summary>
    internal void Input(string line)
    {
        JsonObject? e;
        try { e = JsonNode.Parse(line) as JsonObject; }
        catch (System.Text.Json.JsonException) { return; }
        if (e == null) return;
        string S(string k) => e[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
        int N(string k) => e[k] is JsonValue v && v.TryGetValue<double>(out var d) ? (int)d : 0;
        lock (Gate)
        {
            if (e["t"] is JsonValue t && t.TryGetValue<double>(out var stamp)) Frame.Echo((ulong)stamp);
            if (e.ContainsKey("mouse")) Mouse(S("mouse"), new Point(N("x"), N("y")), new Click(ButtonOf(S("button")), N("clicks")), N("dx"), N("dy"));
            else if (e.ContainsKey("key")) Key((uint)N("sc"), e["ext"] is JsonValue x && x.TryGetValue<bool>(out var ext) && ext, N("mods"), S("key") == "down");
            else if (e.ContainsKey("text")) Panel?.Type(S("text"));
            else if (e.ContainsKey("clipboard")) Clipboard.Copied(S("clipboard"));
            else if (e.ContainsKey("stats")) Tell(e);   // the host's numbers: the desktop's taskbar shows them
            else if (e.ContainsKey("window")) Windows.ById(N("id"))?.Command(S("window"), e);
            Frame.Send();
            Flush();
        }
    }

    private static uint ButtonOf(string name) => name switch
    {
        "right" => 0x111, "middle" => 0x112,
        "back" => 0x113, "forward" => 0x114,   // BTN_SIDE, BTN_EXTRA: Chromium goes back, forward
        _ => 0x110,
    };

    /// <summary>What is at <paramref name="at"/>: the open panel, a menu, the windows.</summary>
    private IPart? Hit(Point at)
    {
        if (Panel is { } p && p.Picture.Rect.Contains(at.X, at.Y)) return p;
        return Popups.Hit(at.X, at.Y) ?? Windows.Hit(at.X, at.Y);
    }

    private void Mouse(string kind, Point at, Click click, int dx, int dy)
    {
        if (Grab is { } grab)
        {
            // holding a window: moves drag it, the button coming up lets go
            if (kind == "move") grab.Drag(at);
            if (kind != "up") return;
            grab.Release();
            Grab = null;
            Pointer.Move(at, Hit(at)?.Target);
            return;
        }
        var part = Hit(at);
        Pointer.Move(at, part?.Target);
        Pointer.Ours(part?.Cursor);
        if (!ReferenceEquals(part, hovered)) hovered?.Out();
        hovered = part;
        part?.Over(at);
        switch (kind)
        {
            case "down":
                if (!ReferenceEquals(part, Panel)) Panel?.Close();
                if (Windows.Desktop is { } desktop && !desktop.IsAbove(at.X, at.Y)) desktop.Lower();
                part?.Down(click);
                if (part?.Target != null) Pointer.Button(click.Button, true);
                break;
            case "up":
                if (Pointer.Holding) Pointer.Button(click.Button, false);
                part?.Up(click);
                Pressed = null;
                break;
            case "wheel":
                Pointer.Wheel(dx, dy);
                break;
        }
    }

    private void Key(uint scancode, bool extended, int mods, bool down)
    {
        if (Panel is { } panel && panel.Key(scancode, extended, mods, down)) return;
        if (Keyboard.Evdev(scancode, extended) is { } key) Keyboard.Key(key, down);
    }

    /// <summary>The page window <paramref name="id"/> shows (for its address field).</summary>
    internal void Url(int id, string url)
    {
        lock (Gate)
            if (Windows.ById(id) is { } window) window.Url = url;
    }

    internal void Stop()
    {
        running = false;
        told.Writer.TryComplete();
        try { listener.Dispose(); } catch (ObjectDisposedException) { }
        lock (Gate)
            foreach (var client in clients.ToList()) client.Close();
    }
}
