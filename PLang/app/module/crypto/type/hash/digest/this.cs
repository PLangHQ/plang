namespace app.module.crypto.type.hash.digest;

/// <summary>
/// A digest being taken — its kind's algorithm over bytes as they come (<see cref="Add"/>), finished into the
/// <see cref="Hash"/>. A write-only stream, so anything that writes its bytes (a value pouring itself, a download's
/// body as it arrives) feeds it without being held whole. Made by its kind (<c>kind.Digest()</c>).
/// </summary>
public sealed class @this : System.IO.Stream
{
    private readonly kind.@this _kind;
    private readonly System.Action<byte[], int, int> _add;
    private readonly System.Func<byte[]> _finish;
    private hash.@this? _hash;

    internal @this(kind.@this kind, System.Action<byte[], int, int> add, System.Func<byte[]> finish)
    {
        _kind = kind;
        _add = add;
        _finish = finish;
    }

    /// <summary>Adds <paramref name="bytes"/> to the digest.</summary>
    public @this Add(byte[] bytes)
    {
        Write(bytes, 0, bytes.Length);
        return this;
    }

    /// <summary>The finished digest, with its algorithm; nothing is added after it is read.</summary>
    public hash.@this Hash => _hash ??= new hash.@this(_finish(), _kind);

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_hash != null) throw new System.InvalidOperationException("the digest is finished: nothing is added after its hash is read");
        _add(buffer, offset, count);
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new System.NotSupportedException("a digest has no length");
    public override long Position
    {
        get => throw new System.NotSupportedException("a digest has no position");
        set => throw new System.NotSupportedException("a digest has no position");
    }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new System.NotSupportedException("a digest is only written");
    public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new System.NotSupportedException("a digest has no position");
    public override void SetLength(long value) => throw new System.NotSupportedException("a digest has no length");
}
