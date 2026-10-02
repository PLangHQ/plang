using System.Buffers.Binary;
using System.Text;

namespace app.module.screen.type.screen.display.code;

/// <summary>
/// One request as it came over the wire: [u32 object][u16 opcode][u16 size] then its arguments,
/// read in order — each 4-byte aligned; a string or array is [u32 length] then its bytes. File
/// descriptors don't travel in the bytes: they came alongside, in order.
/// </summary>
internal sealed class Args
{
    private readonly Client client;
    private readonly byte[] data;
    private int at;

    internal Args(Client client, byte[] data, int start)
    {
        this.client = client;
        this.data = data;
        at = start;
    }

    internal int Int() { var v = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at)); at += 4; return v; }
    internal uint Uint() { var v = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at)); at += 4; return v; }
    internal double Fixed() => Int() / 256.0;
    internal uint NewId() => Uint();
    internal int Fd() => client.Fds.Count > 0 ? client.Fds.Dequeue() : -1;

    internal string? String()
    {
        var length = (int)Uint();
        if (length == 0) return null;
        var s = Encoding.UTF8.GetString(data, at, length - 1);   // without its NUL
        at += (length + 3) & ~3;
        return s;
    }

    internal byte[] Array()
    {
        var length = (int)Uint();
        var a = data.AsSpan(at, length).ToArray();
        at += (length + 3) & ~3;
        return a;
    }

    /// <summary>An object argument: the client's object with that id, if it is a <typeparamref name="T"/>.</summary>
    internal T? Object<T>() where T : Resource => client.Find(Uint()) as T;
}

/// <summary>An event being written to a client, sent with <see cref="Send"/>.</summary>
internal sealed class Event
{
    private readonly Resource target;
    private readonly ushort opcode;
    private readonly MemoryStream body = new();
    private List<int>? fds;

    internal Event(Resource target, ushort opcode)
    {
        this.target = target;
        this.opcode = opcode;
    }

    internal Event Int(int v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteInt32LittleEndian(b, v); body.Write(b); return this; }
    internal Event Uint(uint v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); body.Write(b); return this; }
    internal Event Fixed(double v) => Int(unchecked((int)Math.Round(v * 256.0)));
    internal Event Object(Resource? r) => Uint(r?.Id ?? 0);
    internal Event NewId(Resource r) => Uint(r.Id);

    internal Event String(string? s)
    {
        if (s == null) return Uint(0);
        var bytes = Encoding.UTF8.GetBytes(s);
        Uint((uint)bytes.Length + 1);
        body.Write(bytes);
        body.WriteByte(0);
        Pad(bytes.Length + 1);
        return this;
    }

    internal Event Array(byte[] a)
    {
        Uint((uint)a.Length);
        body.Write(a);
        Pad(a.Length);
        return this;
    }

    internal Event Fd(int fd)
    {
        (fds ??= []).Add(fd);
        return this;
    }

    private void Pad(int length)
    {
        for (var i = length; (i & 3) != 0; i++) body.WriteByte(0);
    }

    internal void Send()
    {
        if (!target.Alive) return;
        var size = 8 + (int)body.Length;
        var message = new byte[size];
        BinaryPrimitives.WriteUInt32LittleEndian(message, target.Id);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(4), (uint)(size << 16) | opcode);
        body.Position = 0;
        body.Read(message, 8, size - 8);
        target.Client.Write(message, fds);
    }
}
