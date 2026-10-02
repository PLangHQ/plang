namespace app.module.http.code;

/// <summary>
/// A body as it moves — a download's response read, or an upload's content sent: at most its cap of bytes
/// (ResponseTooLarge past it), never slower than a kilobyte a second over thirty (SlowResponse), its progress reported
/// every half second and once more when it is done — and each chunk read also written into a tee when one is given (a
/// digest taken as the body arrives). Whoever reads it decides where the body goes: into memory, a file, the wire.
/// </summary>
internal sealed class body : System.IO.Stream
{
    private readonly System.IO.Stream _source;
    private readonly long? _total;
    private readonly global::app.type.item.size.@this? _max;
    private readonly bool _sent;
    private readonly System.Func<global::app.data.@this, Task>? _report;
    private readonly System.IO.Stream? _tee;
    private readonly actor.context.@this _context;
    private long _read;
    private bool _done;
    private DateTimeOffset _lastReport = DateTimeOffset.UtcNow;
    private DateTimeOffset _throughputStart = DateTimeOffset.UtcNow;
    private long _throughputBytes;

    /// <summary>A body read from <paramref name="source"/>: <paramref name="total"/> bytes when the other side said, at
    /// most <paramref name="max"/> (none: no cap), <paramref name="sent"/> when it is an upload's.</summary>
    public body(System.IO.Stream source, long? total, global::app.type.item.size.@this? max, bool sent,
        System.Func<global::app.data.@this, Task>? report, System.IO.Stream? tee, actor.context.@this context)
    {
        _source = source;
        _total = total;
        _max = max;
        _sent = sent;
        _report = report;
        _tee = tee;
        _context = context;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        var count = await _source.ReadAsync(buffer, ct);
        if (count == 0) return 0;
        _read += count;
        if (_max != null && _read > _max.Value)
            await Refuse(new global::app.error.Error($"Download exceeds maximum size of {_max}", "ResponseTooLarge", 413));
        if (_tee != null) await _tee.WriteAsync(buffer[..count], ct);

        _throughputBytes += count;
        var elapsed = (DateTimeOffset.UtcNow - _throughputStart).TotalSeconds;
        if (elapsed >= 30)
        {
            var bytesPerSec = _throughputBytes / elapsed;
            if (bytesPerSec < 1024)
                await Refuse(new global::app.error.Error(
                    $"Transfer too slow ({bytesPerSec:F0} bytes/sec) — possible slow-loris attack", "SlowResponse", 408));
            _throughputStart = DateTimeOffset.UtcNow;
            _throughputBytes = 0;
        }

        if (_report != null && (DateTimeOffset.UtcNow - _lastReport).TotalMilliseconds >= 500)
        {
            _lastReport = DateTimeOffset.UtcNow;
            await _report(_context.Ok(Progress));
        }
        return count;
    }

    /// <summary>The last report: how far the body came, carrying <paramref name="error"/> when it didn't go well (a
    /// hash that doesn't match, a cap passed). Sent once; a later call is nothing.</summary>
    internal async Task Done(global::app.error.Error? error = null)
    {
        if (_done || _report == null) return;
        _done = true;
        var last = new global::app.data.@this("progress", Progress, context: _context);
        if (error != null) last.Fail(error);
        await _report(last);
    }

    // How far the body has come.
    private global::app.module.http.type.progress.@this Progress => new(_read, _total, _sent, _context);

    // The body stops here: the last report carries why, then the read fails with it.
    private async Task Refuse(global::app.error.Error error)
    {
        await Done(error);
        throw new global::app.error.AppException(error);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

    public override int Read(byte[] buffer, int offset, int count)
        => throw new System.NotSupportedException("a body is read asynchronously");

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new System.NotSupportedException("a body's length is known only when read");
    public override long Position
    {
        get => _read;
        set => throw new System.NotSupportedException("a body is read once, in order");
    }
    public override void Flush() { }
    public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new System.NotSupportedException("a body is read once, in order");
    public override void SetLength(long value) => throw new System.NotSupportedException("a body is read once, in order");
    public override void Write(byte[] buffer, int offset, int count) => throw new System.NotSupportedException("a body is only read");
}
