namespace app.module.screen.type.screen.display.code;

/// <summary>
/// The stream behind the shell's <c>errorlog</c> channel: what is written to it (one framed message per write — an error
/// as json) goes up to the host whole, as an error to keep (kind 14). The channel is the door the shell names; nothing
/// here reads what it carries.
/// </summary>
internal sealed class Errorlog(Display display) : Stream
{
    private readonly MemoryStream _message = new();

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Write(byte[] buffer, int offset, int count) => _message.Write(buffer, offset, count);

    // the channel flushes once a message is whole (its end written): up it goes, without that end
    public override void Flush()
    {
        if (_message.Length == 0) return;
        var whole = _message.ToArray().AsSpan();
        while (whole.Length > 0 && whole[^1] is (byte)'\n' or (byte)'\r') whole = whole[..^1];
        if (whole.Length > 0) display.Error(whole);
        _message.SetLength(0);
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
