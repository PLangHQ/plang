namespace app.module.screen.type.screen.display.code;

/// <summary>
/// The clipboard: what is copied — text a client offers, or the host's text — offered to the
/// client with the keyboard. A client's copy is read (through a pipe, on a thread) and sent to the
/// host; the host's copy is written to whichever client pastes it. plang-screen's own copy (the
/// address field's) is text and a value: a paste gets the text; a page that asks for the value
/// (Writer's Ctrl+Shift+V, <c>{"window":"paste"}</c>) gets <see cref="Value"/>.
/// </summary>
internal sealed class Clipboard(Display display)
{
    /// <summary>The text types offered and asked for, best first.</summary>
    internal static readonly string[] Text = ["text/plain;charset=utf-8", "UTF8_STRING", "text/plain", "STRING"];

    private readonly List<WlDataDevice> devices = new();
    private WlDataSource? source;      // a client's copy …
    private string? host;              // … or the host's, or plang-screen's text
    private string? value;             // plang-screen's copy as a value (json)

    /// <summary>The host's clipboard text (the address field pastes it too).</summary>
    internal string Host => host ?? "";

    /// <summary>The clipboard as a value: plang-screen's copy's (json), else its text.</summary>
    internal string Value => value ?? host ?? "";

    internal void Add(WlDataDevice device)
    {
        devices.Add(device);
        if (display.Keyboard.Focus?.Client == device.Client) Offer(device);
    }

    internal void Remove(WlDataDevice device) => devices.Remove(device);

    /// <summary>A client copied: its text goes to the host.</summary>
    internal void Copied(WlDataSource? copied)
    {
        if (source != null && !ReferenceEquals(source, copied)) source.Cancelled();
        source = copied;
        host = null;
        value = null;
        if (copied?.Mime(Text) is { } mime)
        {
            var (read, write) = Native.Pipe();
            if (read >= 0)
            {
                copied.Send(mime, write);
                _ = Task.Run(() =>
                {
                    var text = System.Text.Encoding.UTF8.GetString(Native.ReadAll(read, 16 << 20));
                    Native.close(read);
                    lock (display.Gate) display.Frame.Clipboard(text);
                });
            }
        }
        Offer(display.Keyboard.Focus?.Client);
    }

    /// <summary>The host copied: that text is the clipboard here now. (The host telling back what
    /// plang-screen copied keeps its value.)</summary>
    internal void Copied(string text)
    {
        if (value != null && text == host) return;
        source?.Cancelled();
        source = null;
        host = text;
        value = null;
        Offer(display.Keyboard.Focus?.Client);
    }

    /// <summary>plang-screen copied (the address field): <paramref name="text"/> here and on the host's
    /// clipboard, and <paramref name="json"/> as its value.</summary>
    internal void Copied(string text, string json)
    {
        source?.Cancelled();
        source = null;
        host = text;
        value = json;
        display.Frame.Clipboard(text);
        Offer(display.Keyboard.Focus?.Client);
    }

    /// <summary>The clipboard, offered to <paramref name="client"/>'s devices (the client with the keyboard).</summary>
    internal void Offer(Client? client)
    {
        foreach (var device in devices.Where(d => d.Client == client && d.Alive).ToList()) Offer(device);
    }

    private void Offer(WlDataDevice device)
    {
        IReadOnlyList<string>? types = source != null ? source.Types : host != null ? Text : null;
        if (types == null) { device.Selection(null); return; }
        var offer = new WlDataOffer(device.Client, device.Client.ServerId());
        device.DataOffer(offer);
        foreach (var type in types) offer.Offer(type);
        device.Selection(offer);
    }

    /// <summary>A client pastes: the copy is written into its pipe.</summary>
    internal void Paste(string mime, int fd)
    {
        if (source is { Alive: true } s) { s.Send(mime, fd); return; }
        var text = host ?? "";
        _ = Task.Run(() =>
        {
            Native.Write(fd, System.Text.Encoding.UTF8.GetBytes(text));
            Native.close(fd);
        });
    }
}

/// <summary>wl_data_device_manager: sources (a client's copy) and devices (its clipboard).</summary>
internal sealed class WlDataDeviceManager(Client client, uint id, uint version) : Resource(client, id, version)
{
    internal override void Request(ushort opcode, Request args)
    {
        switch (opcode)
        {
            case 0: _ = new WlDataSource(Client, args.NewId(), Version); break;
            case 1: _ = new WlDataDevice(Client, args.NewId(), Version); break;
        }
    }
}

/// <summary>wl_data_source: what a client copied, in the types it offers.</summary>
internal sealed class WlDataSource(Client client, uint id, uint version) : Resource(client, id, version)
{
    internal List<string> Types { get; } = new();

    internal override void Request(ushort opcode, Request args)
    {
        switch (opcode)
        {
            case 0: if (args.String() is { } type) Types.Add(type); break;
            case 1: Destroy(); break;
        }
    }

    /// <summary>The first of <paramref name="wanted"/> it offers.</summary>
    internal string? Mime(IEnumerable<string> wanted) => wanted.FirstOrDefault(Types.Contains);

    /// <summary>Asks the client to write its copy, as <paramref name="mime"/>, into <paramref name="fd"/>.</summary>
    internal void Send(string mime, int fd) => Event(1).String(mime).Fd(fd).Send();

    internal void Cancelled()
    {
        if (Alive) Event(2).Send();
    }
}

/// <summary>wl_data_device: a client's clipboard.</summary>
internal sealed class WlDataDevice : Resource
{
    internal WlDataDevice(Client client, uint id, uint version) : base(client, id, version) => Display.Clipboard.Add(this);

    internal override void Request(ushort opcode, Request args)
    {
        switch (opcode)
        {
            case 1: Display.Clipboard.Copied(args.Object<WlDataSource>()); break;   // set_selection
            case 2: Destroy(); break;
        }
    }

    protected override void Destroyed() => Display.Clipboard.Remove(this);

    internal void DataOffer(WlDataOffer offer) => Event(0).NewId(offer).Send();
    internal void Selection(WlDataOffer? offer) => Event(5).Object(offer).Send();
}

/// <summary>wl_data_offer: the clipboard, offered to a client (made by the server).</summary>
internal sealed class WlDataOffer(Client client, uint id) : Resource(client, id, 3)
{
    internal override void Request(ushort opcode, Request args)
    {
        switch (opcode)
        {
            case 1:   // receive(mime, fd): paste
                var mime = args.String() ?? "";
                var fd = args.Fd();
                if (fd >= 0) Display.Clipboard.Paste(mime, fd);
                break;
            case 2: Destroy(); break;
        }
    }

    internal void Offer(string mime) => Event(0).String(mime).Send();
}
