namespace PLang.Tests.App.ChannelsTests;

// Stage 2 — Channel.Stream concrete (the existing Channel refactored).
// Architect: stage-2-stream-channel.md.

public class Stage2_StreamChannelTests
{
    [Test]
    public async Task StreamChannel_WriteCore_WritesDataViaSerializer()
    {
        await using var app = new global::app.@this("/test", autoWireConsoleChannels: false);
        var captureStream = new MemoryStream();
        var ch = new StreamChannel("c", captureStream, ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" };
        app.User.Channels.Register(ch);

        var result = await ch.WriteCore(Data.Ok("hello"));
        await Assert.That(result.Success).IsTrue();

        var written = global::System.Text.Encoding.UTF8.GetString(captureStream.ToArray());
        await Assert.That(written.Contains("hello")).IsTrue();
    }

    [Test]
    public async Task StreamChannel_ReadCore_ReadsBytes_DeserialisesViaMime()
    {
        var ms = new MemoryStream(global::System.Text.Encoding.UTF8.GetBytes("hello"));
        var ch = new StreamChannel("c", ms, ChannelDirection.Input, ownsStream: false)
        { Mime = "text/plain" };
        var result = await ch.ReadCore();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value as string).IsEqualTo("hello");
    }

    [Test]
    public async Task StreamChannel_MemoryFactory_CreatesBidirectionalChannel()
    {
        var ch = StreamChannel.Memory("m");
        await Assert.That(ch.Direction).IsEqualTo(ChannelDirection.Bidirectional);
        await Assert.That(ch.CanRead).IsTrue();
        await Assert.That(ch.CanWrite).IsTrue();
    }

    [Test]
    public async Task StreamChannel_OutputFactory_CreatesWriteOnlyChannel()
    {
        var ch = StreamChannel.Output("o", new MemoryStream());
        await Assert.That(ch.Direction).IsEqualTo(ChannelDirection.Output);
        await Assert.That(ch.CanWrite).IsTrue();
        await Assert.That(ch.CanRead).IsFalse();
        var read = await ch.ReadCore();
        await Assert.That(read.Success).IsFalse();
        await Assert.That(read.Error!.Key).IsEqualTo("ChannelWriteOnly");
    }

    [Test]
    public async Task StreamChannel_InputFactory_CreatesReadOnlyChannel()
    {
        var ch = StreamChannel.Input("i", new MemoryStream());
        await Assert.That(ch.Direction).IsEqualTo(ChannelDirection.Input);
        await Assert.That(ch.CanRead).IsTrue();
        await Assert.That(ch.CanWrite).IsFalse();
        var write = await ch.WriteCore(Data.Ok("x"));
        await Assert.That(write.Success).IsFalse();
        await Assert.That(write.Error!.Key).IsEqualTo("ChannelReadOnly");
    }

    [Test]
    public async Task StreamChannel_WriteCore_FailsWithWriteError_OnUnderlyingStreamThrow()
    {
        await using var app = new global::app.@this("/test", autoWireConsoleChannels: false);
        var ch = new StreamChannel("c", new ThrowingStream(throwOnWrite: true), ChannelDirection.Output, ownsStream: false);
        app.User.Channels.Register(ch);
        var result = await ch.WriteCore(Data.Ok("x"));
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("WriteError");
    }

    [Test]
    public async Task StreamChannel_ReadCore_FailsWithReadError_OnUnderlyingStreamThrow()
    {
        var ch = new StreamChannel("c", new ThrowingStream(throwOnRead: true), ChannelDirection.Input, ownsStream: false);
        var result = await ch.ReadCore();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ReadError");
    }

    [Test]
    public async Task StreamChannel_Ask_BlocksOnStdinUntilInputArrives()
    {
        var ms = new MemoryStream(global::System.Text.Encoding.UTF8.GetBytes("answer\n"));
        var ch = new StreamChannel("i", ms, ChannelDirection.Bidirectional, ownsStream: false)
        { Mime = "text/plain" };
        var result = await ch.AskCore(Data.Ok((object?)null));
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value as string).IsEqualTo("answer");
    }

    [Test]
    public async Task StreamChannel_Ask_HonoursConfiguredEncoding()
    {
        // Auditor v1 A3 regression: AskCore was hard-coded to UTF-8 (StreamReader
        // ctor with no encoding) and ignored the channel's Encoding property.
        // With the fix it routes through ResolveEncoding() like ReadAllTextAsync.
        // Bytes 0xE9 0x0A = "é\n" in iso-8859-1; 0xE9 alone is an invalid UTF-8
        // start byte, so a UTF-8 reader would yield U+FFFD instead of 'é'.
        var bytes = new byte[] { 0xE9, 0x0A };
        var ms = new MemoryStream(bytes);
        var ch = new StreamChannel("i", ms, ChannelDirection.Bidirectional, ownsStream: false)
        {
            Mime = "text/plain",
            Encoding = "iso-8859-1"
        };
        var result = await ch.AskCore(Data.Ok((object?)null));
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value as string).IsEqualTo("é");
    }

    [Test]
    public async Task StreamChannel_Ask_TimesOutPerChannelTimeoutConfig()
    {
        // Pipe with no writer → Read blocks forever; Timeout=PT1S triggers AskTimeout.
        var pipe = new BlockingStream();
        var ch = new StreamChannel("i", pipe, ChannelDirection.Input, ownsStream: false)
        {
            Mime = "text/plain",
            Timeout = TimeSpan.FromMilliseconds(100)
        };
        var result = await ch.AskCore(Data.Ok((object?)null));
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("AskTimeout");
    }

    [Test]
    public async Task StreamChannel_OwnsStreamTrue_DisposesUnderlyingStream()
    {
        var ms = new TrackingMemoryStream();
        var ch = new StreamChannel("c", ms, ChannelDirection.Bidirectional, ownsStream: true);
        await ch.DisposeAsync();
        await Assert.That(ms.Disposed).IsTrue();
    }

    [Test]
    public async Task StreamChannel_OwnsStreamFalse_LeavesUnderlyingStreamOpen()
    {
        var ms = new TrackingMemoryStream();
        var ch = new StreamChannel("c", ms, ChannelDirection.Bidirectional, ownsStream: false);
        await ch.DisposeAsync();
        await Assert.That(ms.Disposed).IsFalse();
    }

    // --- Test helpers ---

    private sealed class TrackingMemoryStream : MemoryStream
    {
        public bool Disposed { get; private set; }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class ThrowingStream : Stream
    {
        private readonly bool _throwOnWrite;
        private readonly bool _throwOnRead;
        public ThrowingStream(bool throwOnWrite = false, bool throwOnRead = false)
        {
            _throwOnWrite = throwOnWrite;
            _throwOnRead = throwOnRead;
        }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get => 0; set { } }
        public override void Flush() { }
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_throwOnWrite) throw new IOException("simulated write failure");
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_throwOnRead) throw new IOException("simulated read failure");
            return 0;
        }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_throwOnWrite) return Task.FromException(new IOException("simulated write failure"));
            return Task.CompletedTask;
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_throwOnWrite) return ValueTask.FromException(new IOException("simulated write failure"));
            return ValueTask.CompletedTask;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_throwOnRead) return Task.FromException<int>(new IOException("simulated read failure"));
            return Task.FromResult(0);
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_throwOnRead) return ValueTask.FromException<int>(new IOException("simulated read failure"));
            return ValueTask.FromResult(0);
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    private sealed class BlockingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get => 0; set { } }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            // Block forever until cancelled
            Thread.Sleep(global::System.Threading.Timeout.Infinite);
            return 0;
        }
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            await Task.Delay(global::System.Threading.Timeout.Infinite, ct);
            return 0;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            await Task.Delay(global::System.Threading.Timeout.Infinite, ct);
            return 0;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // F5 regression — non-UTF-8 Encoding property must be honored by the convenience
    // text helpers, not silently coerced to UTF-8.
    [Test]
    public async Task StreamChannel_WriteTextAsync_HonorsLatin1Encoding()
    {
        var capture = new MemoryStream();
        var ch = new StreamChannel("c", capture, ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain", Encoding = "iso-8859-1" };

        // 'é' is one byte in latin-1 (0xE9) but two bytes in UTF-8.
        await ch.WriteTextAsync("é");
        var bytes = capture.ToArray();

        await Assert.That(bytes.Length).IsEqualTo(1);
        await Assert.That(bytes[0]).IsEqualTo((byte)0xE9);
    }

    [Test]
    public async Task StreamChannel_ReadAllTextAsync_HonorsLatin1Encoding()
    {
        var ms = new MemoryStream(new byte[] { 0xE9 });
        var ch = new StreamChannel("c", ms, ChannelDirection.Input, ownsStream: false)
        { Mime = "text/plain", Encoding = "iso-8859-1" };

        var text = await ch.ReadAllTextAsync();
        await Assert.That(text).IsEqualTo("é");
    }

    [Test]
    public async Task StreamChannel_UnknownEncoding_FallsBackToUtf8()
    {
        var capture = new MemoryStream();
        var ch = new StreamChannel("c", capture, ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain", Encoding = "totally-not-an-encoding" };
        await ch.WriteTextAsync("hi");
        await Assert.That(capture.ToArray()).IsEquivalentTo(new byte[] { (byte)'h', (byte)'i' });
    }
}
