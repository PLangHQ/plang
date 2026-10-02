namespace app.module.http.code;

/// <summary>
/// A response's body as it is read: at most its cap of bytes (ResponseTooLarge past it), never slower than a
/// kilobyte a second over thirty (SlowResponse), its progress reported every half second — and each chunk read also
/// written into a tee when one is given (a digest taken as the body arrives). Whoever reads it decides where the body
/// goes: into memory, or a file.
/// </summary>
internal sealed class body : System.IO.Stream
{
    private readonly System.IO.Stream _source;
    private readonly long? _total;
    private readonly long _max;
    private readonly System.Func<TransferProgress, Task>? _report;
    private readonly System.IO.Stream? _tee;
    private long _read;
    private DateTimeOffset _lastReport = DateTimeOffset.UtcNow;
    private DateTimeOffset _throughputStart = DateTimeOffset.UtcNow;
    private long _throughputBytes;

    public body(System.IO.Stream source, long? total, long max, System.Func<TransferProgress, Task>? report, System.IO.Stream? tee)
    {
        _source = source;
        _total = total;
        _max = max;
        _report = report;
        _tee = tee;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        var count = await _source.ReadAsync(buffer, ct);
        if (count == 0) return 0;
        _read += count;
        if (_read > _max)
            throw new global::app.error.AppException(
                $"Download exceeds maximum size of {Default.FormatBytes(_max)}", "ResponseTooLarge", 413);
        if (_tee != null) await _tee.WriteAsync(buffer[..count], ct);

        _throughputBytes += count;
        var elapsed = (DateTimeOffset.UtcNow - _throughputStart).TotalSeconds;
        if (elapsed >= 30)
        {
            var bytesPerSec = _throughputBytes / elapsed;
            if (bytesPerSec < 1024)
                throw new global::app.error.AppException(
                    $"Transfer too slow ({bytesPerSec:F0} bytes/sec) — possible slow-loris attack", "SlowResponse", 408);
            _throughputStart = DateTimeOffset.UtcNow;
            _throughputBytes = 0;
        }

        if (_report != null && (DateTimeOffset.UtcNow - _lastReport).TotalMilliseconds >= 500)
        {
            _lastReport = DateTimeOffset.UtcNow;
            await _report(new TransferProgress
            {
                BytesTransferred = _read,
                TotalBytes = _total,
                Percentage = _total > 0 ? (double)_read / _total.Value * 100 : null,
            });
        }
        return count;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

    public override int Read(byte[] buffer, int offset, int count)
        => throw new System.NotSupportedException("a response body is read asynchronously");

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new System.NotSupportedException("a response body's length is known only when read");
    public override long Position
    {
        get => _read;
        set => throw new System.NotSupportedException("a response body is read once, in order");
    }
    public override void Flush() { }
    public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new System.NotSupportedException("a response body is read once, in order");
    public override void SetLength(long value) => throw new System.NotSupportedException("a response body is read once, in order");
    public override void Write(byte[] buffer, int offset, int count) => throw new System.NotSupportedException("a response body is only read");
}
