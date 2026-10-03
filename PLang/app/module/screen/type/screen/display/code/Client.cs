using System.Buffers.Binary;
using System.Net.Sockets;

namespace app.module.screen.type.screen.display.code;

/// <summary>
/// A protocol object a client holds: a surface, a buffer, a keyboard … Each kind owns its own
/// requests (<see cref="Request"/>) and writes its own events.
/// </summary>
internal abstract class Resource
{
    internal Client Client { get; }
    internal uint Id { get; }
    internal uint Version { get; }

    protected Resource(Client client, uint id, uint version)
    {
        Client = client;
        Id = id;
        Version = version;
        client.Add(this);
    }

    internal Display Display => Client.Display;

    /// <summary>Still the client's (not destroyed, client still there).</summary>
    internal bool Alive => !Client.Closed && ReferenceEquals(Client.Find(Id), this);

    internal abstract void Request(ushort opcode, Request args);

    internal Event Event(ushort opcode) => new(this, opcode);

    /// <summary>The client let it go (or went away).</summary>
    internal void Destroy()
    {
        if (!ReferenceEquals(Client.Find(Id), this)) return;
        Client.Remove(this);
        Destroyed();
    }

    /// <summary>The client went away: no one to tell, but what the object holds goes.</summary>
    internal void Lost() => Destroyed();

    protected virtual void Destroyed() { }
}

/// <summary>
/// One connection (a client: Chromium). Its objects by id, the file descriptors that came with its
/// messages, and what is waiting to be sent to it. Reads on a thread of its own; everything it
/// asks is done under the compositor's gate, one message at a time.
/// </summary>
internal sealed class Client
{
    internal Display Display { get; }
    private readonly Socket socket;
    private readonly int fd;
    private readonly Dictionary<uint, Resource> objects = new();
    private uint nextServerId = 0xff000000;
    private readonly MemoryStream output = new();
    private readonly List<int> outputFds = new();

    /// <summary>Descriptors that came with its messages, taken in order by the requests that carry one.</summary>
    internal Queue<int> Fds { get; } = new();
    internal bool Closed { get; private set; }

    internal Client(Display display, Socket socket)
    {
        Display = display;
        this.socket = socket;
        fd = (int)socket.Handle;
        _ = new WlDisplay(this);
    }

    internal Resource? Find(uint id) => objects.GetValueOrDefault(id);
    internal void Add(Resource r) => objects[r.Id] = r;

    /// <summary>A server-made object's id (a clipboard offer): from the server's own range.</summary>
    internal uint ServerId() => nextServerId++;

    internal void Remove(Resource r)
    {
        objects.Remove(r.Id);
        if (r.Id < 0xff000000 && objects.GetValueOrDefault(1u) is WlDisplay wl)
            wl.Deleted(r.Id);   // the client may reuse the id
    }

    /// <summary>Reads and dispatches until the client hangs up.</summary>
    internal void Run()
    {
        var buffer = new byte[65536];
        var pending = new MemoryStream();
        var fds = new Queue<int>();
        try
        {
            while (true)
            {
                var n = Native.Receive(fd, buffer, fds);
                if (n <= 0) break;
                pending.Write(buffer, 0, n);
                var bytes = pending.GetBuffer();
                var length = (int)pending.Length;
                var at = 0;
                lock (Display.Gate)
                {
                    while (fds.Count > 0) Fds.Enqueue(fds.Dequeue());
                    while (length - at >= 8)
                    {
                        var size = (int)(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at + 4)) >> 16);
                        if (size < 8 || length - at < size) break;
                        var id = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
                        var opcode = (ushort)(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at + 4)) & 0xffff);
                        var message = bytes.AsSpan(at, size).ToArray();
                        at += size;
                        if (objects.TryGetValue(id, out var target))
                        {
                            try { target.Request(opcode, new Request(this, message, 8)); }
                            catch (Exception ex)
                            {
                                // one bad request (or a fault of ours) must not end the connection unseen
                                Display.Debug($"wayland: {target.GetType().Name}#{id} request {opcode}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                            }
                        }
                    }
                    Display.Flush();
                }
                // keep what is left of an unfinished message
                var rest = bytes.AsSpan(at, length - at).ToArray();
                pending.SetLength(0);
                pending.Write(rest);
                if (Closed) break;
            }
        }
        finally
        {
            lock (Display.Gate)
            {
                Close();
                Display.Flush();
            }
        }
    }

    /// <summary>An event, queued; one carrying descriptors goes at once, with them.</summary>
    internal void Write(byte[] message, List<int>? fds)
    {
        if (Closed) return;
        output.Write(message);
        if (fds is { Count: > 0 })
        {
            outputFds.AddRange(fds);
            Flush();
        }
    }

    /// <summary>Sends what is queued. Descriptors made for the client (keymap, pipes) are closed here once sent.</summary>
    internal void Flush()
    {
        if (Closed || output.Length == 0) return;
        if (!Native.Send(fd, output.GetBuffer(), (int)output.Length, outputFds)) Closed = true;
        foreach (var sent in outputFds) Native.close(sent);
        outputFds.Clear();
        output.SetLength(0);
        if (Closed) Close();
    }

    /// <summary>The client is gone (or broke the protocol): every object it held goes with it.</summary>
    internal void Close()
    {
        if (objects.Count == 0 && Closed) return;
        Closed = true;
        // newest first: roles (toplevels, popups) go before their surfaces
        foreach (var r in objects.Values.OrderByDescending(r => r.Id).ToList())
        {
            objects.Remove(r.Id);
            r.Lost();
        }
        foreach (var fdLeft in Fds) Native.close(fdLeft);
        Fds.Clear();
        Display.Gone(this);
        try { socket.Dispose(); } catch (ObjectDisposedException) { }
    }
}
