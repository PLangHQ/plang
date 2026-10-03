namespace app.module.archive.code;

/// <summary>
/// A stream whose first bytes have been looked at — what a format is known by — and are still read first: nothing of
/// it is lost to the look.
/// </summary>
internal sealed class peeked : System.IO.Stream
{
    private readonly System.IO.Stream _rest;
    private readonly byte[] _start;
    private int _given;

    /// <summary><paramref name="source"/>, its first bytes (up to <paramref name="count"/>) read ahead.</summary>
    public peeked(System.IO.Stream source, int count = 8)
    {
        _rest = source;
        var start = new byte[count];
        _start = start[..source.ReadAtLeast(start, count, throwOnEndOfStream: false)];
    }

    /// <summary>The bytes it begins with.</summary>
    public ReadOnlySpan<byte> Start => _start;

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_given < _start.Length)
        {
            var some = Math.Min(count, _start.Length - _given);
            Array.Copy(_start, _given, buffer, offset, some);
            _given += some;
            return some;
        }
        return _rest.Read(buffer, offset, count);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (_given < _start.Length)
        {
            var some = Math.Min(buffer.Length, _start.Length - _given);
            _start.AsMemory(_given, some).CopyTo(buffer);
            _given += some;
            return some;
        }
        return await _rest.ReadAsync(buffer, ct);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _rest.Dispose();
        base.Dispose(disposing);
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException("a peeked stream is read once, in order");
    public override long Position
    {
        get => throw new NotSupportedException("a peeked stream is read once, in order");
        set => throw new NotSupportedException("a peeked stream is read once, in order");
    }
    public override void Flush() { }
    public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new NotSupportedException("a peeked stream is read once, in order");
    public override void SetLength(long value) => throw new NotSupportedException("a peeked stream is read once, in order");
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("a peeked stream is only read");
}
