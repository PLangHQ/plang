namespace app.module.archive.type.archive.kind.bundle.cap;

/// <summary>
/// How much an unpack may still write: the most the whole unpack lands, counted as every file's content is read, so a
/// bundle that unpacks to more than it says (a zip bomb) stops at the cap, never after.
/// </summary>
public sealed class @this
{
    private readonly global::app.type.item.size.@this _max;
    private long _written;

    public @this(global::app.type.item.size.@this max) => _max = max;

    /// <summary><paramref name="content"/>, read through this cap.</summary>
    public System.IO.Stream Over(System.IO.Stream content) => new counted(this, content);

    // Counts what is read; past the cap the read fails, naming it.
    private void Add(int count)
    {
        _written += count;
        if (_written > _max.Value)
            throw new global::app.error.AppException(new global::app.error.ServiceError(
                $"the archive unpacks to more than {_max}", "ArchiveTooLarge", 413));
    }

    private sealed class counted(@this cap, System.IO.Stream source) : System.IO.Stream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            var count = await source.ReadAsync(buffer, ct);
            cap.Add(count);
            return count;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = source.Read(buffer, offset, count);
            cap.Add(read);
            return read;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new System.NotSupportedException("a capped content's length is known only when read");
        public override long Position
        {
            get => throw new System.NotSupportedException("a capped content is read once, in order");
            set => throw new System.NotSupportedException("a capped content is read once, in order");
        }
        public override void Flush() { }
        public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new System.NotSupportedException("a capped content is read once, in order");
        public override void SetLength(long value) => throw new System.NotSupportedException("a capped content is read once, in order");
        public override void Write(byte[] buffer, int offset, int count) => throw new System.NotSupportedException("a capped content is only read");
    }
}
