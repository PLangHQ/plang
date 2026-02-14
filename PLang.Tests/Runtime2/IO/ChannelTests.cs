using PLang.Runtime2.IO;
using PLang.SafeFileSystem;
using Path = System.IO.Path;

namespace PLang.Tests.Runtime2.IO;

public class ChannelTests
{
    private static PLangFileSystem CreateFileSystem() => new(Path.GetTempPath(), "");

    [Test]
    public async Task Constructor_SetsProperties()
    {
        using var stream = new MemoryStream();
        using var channel = new Channel("test", stream);

        await Assert.That(channel.Name).IsEqualTo("test");
        await Assert.That(channel.Stream).IsEqualTo(stream);
        await Assert.That(channel.Direction).IsEqualTo(ChannelDirection.Bidirectional);
        await Assert.That(channel.IsOpen).IsTrue();
    }

    [Test]
    public async Task Constructor_SetsDirection()
    {
        using var stream = new MemoryStream();
        using var channel = new Channel("test", stream, ChannelDirection.Input);

        await Assert.That(channel.Direction).IsEqualTo(ChannelDirection.Input);
    }

    [Test]
    public async Task Constructor_SetsCreatedTimestamp()
    {
        var before = DateTime.UtcNow;

        using var stream = new MemoryStream();
        using var channel = new Channel("test", stream);

        var after = DateTime.UtcNow;
        await Assert.That(channel.Created).IsGreaterThanOrEqualTo(before);
        await Assert.That(channel.Created).IsLessThanOrEqualTo(after);
    }

    [Test]
    public async Task Constructor_InitializesMetadata()
    {
        using var stream = new MemoryStream();
        using var channel = new Channel("test", stream);

        await Assert.That(channel.Metadata).IsNotNull();
        await Assert.That(channel.Metadata.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ContentType_CanBeSet()
    {
        using var stream = new MemoryStream();
        using var channel = new Channel("test", stream);

        channel.ContentType = "application/json";

        await Assert.That(channel.ContentType).IsEqualTo("application/json");
    }

    [Test]
    public async Task Input_CreatesInputChannel()
    {
        using var stream = new MemoryStream();
        using var channel = Channel.Input("test", stream);

        await Assert.That(channel.Direction).IsEqualTo(ChannelDirection.Input);
    }

    [Test]
    public async Task Output_CreatesOutputChannel()
    {
        using var stream = new MemoryStream();
        using var channel = Channel.Output("test", stream);

        await Assert.That(channel.Direction).IsEqualTo(ChannelDirection.Output);
    }

    [Test]
    public async Task Memory_CreatesMemoryBackedChannel()
    {
        using var channel = Channel.Memory("test");

        await Assert.That(channel.Stream).IsTypeOf<MemoryStream>();
        await Assert.That(channel.Direction).IsEqualTo(ChannelDirection.Bidirectional);
    }

    [Test]
    public async Task Memory_WithDirection_SetsDirection()
    {
        using var channel = Channel.Memory("test", ChannelDirection.Output);

        await Assert.That(channel.Direction).IsEqualTo(ChannelDirection.Output);
    }

    [Test]
    public async Task CanRead_BidirectionalChannel_ReturnsTrue()
    {
        using var channel = Channel.Memory("test", ChannelDirection.Bidirectional);

        await Assert.That(channel.CanRead).IsTrue();
    }

    [Test]
    public async Task CanRead_InputChannel_ReturnsTrue()
    {
        using var stream = new MemoryStream();
        using var channel = Channel.Input("test", stream);

        await Assert.That(channel.CanRead).IsTrue();
    }

    [Test]
    public async Task CanRead_OutputChannel_ReturnsFalse()
    {
        using var stream = new MemoryStream();
        using var channel = Channel.Output("test", stream);

        await Assert.That(channel.CanRead).IsFalse();
    }

    [Test]
    public async Task CanRead_ClosedChannel_ReturnsFalse()
    {
        using var channel = Channel.Memory("test");
        channel.Close();

        await Assert.That(channel.CanRead).IsFalse();
    }

    [Test]
    public async Task CanWrite_BidirectionalChannel_ReturnsTrue()
    {
        using var channel = Channel.Memory("test", ChannelDirection.Bidirectional);

        await Assert.That(channel.CanWrite).IsTrue();
    }

    [Test]
    public async Task CanWrite_OutputChannel_ReturnsTrue()
    {
        using var stream = new MemoryStream();
        using var channel = Channel.Output("test", stream);

        await Assert.That(channel.CanWrite).IsTrue();
    }

    [Test]
    public async Task CanWrite_InputChannel_ReturnsFalse()
    {
        using var stream = new MemoryStream();
        using var channel = Channel.Input("test", stream);

        await Assert.That(channel.CanWrite).IsFalse();
    }

    [Test]
    public async Task CanWrite_ClosedChannel_ReturnsFalse()
    {
        using var channel = Channel.Memory("test");
        channel.Close();

        await Assert.That(channel.CanWrite).IsFalse();
    }

    [Test]
    public async Task WriteBytesAsync_WritesData()
    {
        using var channel = Channel.Memory("test");
        var data = new byte[] { 1, 2, 3 };

        await channel.WriteBytesAsync(data);

        channel.Stream.Position = 0;
        var buffer = new byte[3];
        await channel.Stream.ReadAsync(buffer);
        await Assert.That(buffer).IsEquivalentTo(data);
    }

    [Test]
    public async Task WriteBytesAsync_CannotWriteToInputChannel_ThrowsException()
    {
        using var stream = new MemoryStream();
        using var channel = Channel.Input("test", stream);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await channel.WriteBytesAsync(new byte[] { 1 }));
    }

    [Test]
    public async Task ReadAllBytesAsync_ReadsData()
    {
        using var channel = Channel.Memory("test");
        var data = new byte[] { 1, 2, 3 };
        await channel.WriteBytesAsync(data);

        var result = await channel.ReadAllBytesAsync();

        await Assert.That(result).IsEquivalentTo(data);
    }

    [Test]
    public async Task ReadAllBytesAsync_CannotReadFromOutputChannel_ThrowsException()
    {
        using var stream = new MemoryStream();
        using var channel = Channel.Output("test", stream);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await channel.ReadAllBytesAsync());
    }

    [Test]
    public async Task WriteTextAsync_WritesText()
    {
        using var channel = Channel.Memory("test");

        await channel.WriteTextAsync("hello world");

        var result = await channel.ReadAllTextAsync();
        await Assert.That(result).IsEqualTo("hello world");
    }

    [Test]
    public async Task ReadAllTextAsync_ReadsText()
    {
        using var channel = Channel.Memory("test");
        await channel.WriteTextAsync("hello world");

        var result = await channel.ReadAllTextAsync();

        await Assert.That(result).IsEqualTo("hello world");
    }

    [Test]
    public async Task Close_SetsIsOpenFalse()
    {
        using var channel = Channel.Memory("test");

        channel.Close();

        await Assert.That(channel.IsOpen).IsFalse();
    }

    [Test]
    public async Task Close_CalledTwice_DoesNotThrow()
    {
        using var channel = Channel.Memory("test");

        channel.Close();
        channel.Close();

        await Assert.That(channel.IsOpen).IsFalse();
    }

    [Test]
    public async Task Dispose_ClosesChannel()
    {
        var channel = Channel.Memory("test");

        channel.Dispose();

        await Assert.That(channel.IsOpen).IsFalse();
    }

    [Test]
    public async Task DisposeAsync_ClosesChannel()
    {
        var channel = Channel.Memory("test");

        await channel.DisposeAsync();

        await Assert.That(channel.IsOpen).IsFalse();
    }

    [Test]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        var channel = Channel.Memory("test");

        await channel.DisposeAsync();
        await channel.DisposeAsync();

        await Assert.That(channel.IsOpen).IsFalse();
    }

    [Test]
    public async Task Metadata_CanSetAndRetrieveValues()
    {
        using var channel = Channel.Memory("test");

        channel.Metadata["key"] = "value";

        await Assert.That(channel.Metadata["key"]).IsEqualTo("value");
    }

    [Test]
    public async Task Metadata_CaseInsensitiveKeys()
    {
        using var channel = Channel.Memory("test");

        channel.Metadata["Key"] = "value";

        await Assert.That(channel.Metadata["key"]).IsEqualTo("value");
        await Assert.That(channel.Metadata["KEY"]).IsEqualTo("value");
    }

    [Test]
    public async Task File_WithOpenMode_CreatesInputChannel()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await System.IO.File.WriteAllTextAsync(tempFile, "test content");

            using var channel = Channel.File("test", tempFile, CreateFileSystem(), FileMode.Open);

            await Assert.That(channel.Direction).IsEqualTo(ChannelDirection.Input);
            await Assert.That(channel.CanRead).IsTrue();
        }
        finally
        {
            System.IO.File.Delete(tempFile);
        }
    }

    [Test]
    public async Task File_WithCreateMode_CreatesOutputChannel()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            using var channel = Channel.File("test", tempFile, CreateFileSystem(), FileMode.Create);

            await Assert.That(channel.Direction).IsEqualTo(ChannelDirection.Output);
            await Assert.That(channel.CanWrite).IsTrue();
        }
        finally
        {
            System.IO.File.Delete(tempFile);
        }
    }

    [Test]
    public async Task File_WithOpenOrCreateMode_CreatesBidirectionalChannel()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            using var channel = Channel.File("test", tempFile, CreateFileSystem(), FileMode.OpenOrCreate);

            await Assert.That(channel.Direction).IsEqualTo(ChannelDirection.Bidirectional);
        }
        finally
        {
            System.IO.File.Delete(tempFile);
        }
    }

    [Test]
    public async Task OwnsStream_True_DisposesStreamOnClose()
    {
        var stream = new MemoryStream();
        var channel = new Channel("test", stream, ownsStream: true);

        channel.Close();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await Task.Run(() => stream.WriteByte(0));
        });
    }

    [Test]
    public async Task OwnsStream_False_DoesNotDisposeStreamOnClose()
    {
        using var stream = new MemoryStream();
        var channel = new Channel("test", stream, ownsStream: false);

        channel.Close();

        // Stream should still be usable
        stream.WriteByte(0);
        await Assert.That(stream.Length).IsEqualTo(1);
    }
}
