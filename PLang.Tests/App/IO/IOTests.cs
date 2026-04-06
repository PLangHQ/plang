using App;

namespace PLang.Tests.App.IO;

public class IOTests
{
    private static EngineChannels CreateIO()
    {
        var engine = new App.@this("/app");
        return new EngineChannels(engine);
    }

    [Test]
    public async Task Constructor_CreatesWithEngine()
    {
        await using var io = CreateIO();

        // Should not throw
        await Assert.That(io).IsNotNull();
    }

    [Test]
    public async Task StdIn_Constant_IsCorrect()
    {
        await Assert.That(EngineChannels.StdIn).IsEqualTo("stdin");
    }

    [Test]
    public async Task StdOut_Constant_IsCorrect()
    {
        await Assert.That(EngineChannels.StdOut).IsEqualTo("stdout");
    }

    [Test]
    public async Task StdErr_Constant_IsCorrect()
    {
        await Assert.That(EngineChannels.StdErr).IsEqualTo("stderr");
    }

    [Test]
    public async Task Register_AddsChannel()
    {
        await using var io = CreateIO();
        var channel = Channel.Memory("test");

        io.Register(channel);

        await Assert.That(io.Contains("test")).IsTrue();
    }

    [Test]
    public async Task Get_ReturnsRegisteredChannel()
    {
        await using var io = CreateIO();
        var channel = Channel.Memory("test");
        io.Register(channel);

        var result = io.Get("test");

        await Assert.That(result).IsEqualTo(channel);
    }

    [Test]
    public async Task Get_NonexistentChannel_ReturnsNull()
    {
        await using var io = CreateIO();

        var result = io.Get("nonexistent");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Get_CaseInsensitive()
    {
        await using var io = CreateIO();
        var channel = Channel.Memory("Test");
        io.Register(channel);

        await Assert.That(io.Get("test")).IsEqualTo(channel);
        await Assert.That(io.Get("TEST")).IsEqualTo(channel);
    }

    [Test]
    public async Task GetOrCreate_ExistingChannel_ReturnsExisting()
    {
        await using var io = CreateIO();
        var channel = Channel.Memory("test");
        io.Register(channel);

        var result = io.GetOrCreate("test", () => Channel.Memory("different"));

        await Assert.That(result).IsEqualTo(channel);
    }

    [Test]
    public async Task GetOrCreate_NonexistentChannel_CreatesNew()
    {
        await using var io = CreateIO();

        var result = io.GetOrCreate("test", () => Channel.Memory("test"));

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Name).IsEqualTo("test");
    }

    [Test]
    public async Task Contains_ExistingChannel_ReturnsTrue()
    {
        await using var io = CreateIO();
        io.Register(Channel.Memory("test"));

        await Assert.That(io.Contains("test")).IsTrue();
    }

    [Test]
    public async Task Contains_NonexistentChannel_ReturnsFalse()
    {
        await using var io = CreateIO();

        await Assert.That(io.Contains("nonexistent")).IsFalse();
    }

    [Test]
    public async Task RemoveAsync_RemovesChannel()
    {
        await using var io = CreateIO();
        io.Register(Channel.Memory("test"));

        var removed = await io.RemoveAsync("test");

        await Assert.That(removed).IsTrue();
        await Assert.That(io.Contains("test")).IsFalse();
    }

    [Test]
    public async Task RemoveAsync_NonexistentChannel_ReturnsFalse()
    {
        await using var io = CreateIO();

        var removed = await io.RemoveAsync("nonexistent");

        await Assert.That(removed).IsFalse();
    }

    [Test]
    public async Task ChannelNames_ReturnsAllNames()
    {
        await using var io = CreateIO();
        io.Register(Channel.Memory("channel1"));
        io.Register(Channel.Memory("channel2"));

        var names = io.ChannelNames.ToList();

        await Assert.That(names).Contains("channel1");
        await Assert.That(names).Contains("channel2");
    }

    [Test]
    public async Task CreateMemoryChannel_CreatesAndRegistersChannel()
    {
        await using var io = CreateIO();

        var channel = io.CreateMemoryChannel("test");

        await Assert.That(channel).IsNotNull();
        await Assert.That(io.Contains("test")).IsTrue();
    }

    [Test]
    public async Task CreateMemoryChannel_WithDirection_SetsDirection()
    {
        await using var io = CreateIO();

        var channel = io.CreateMemoryChannel("test", ChannelDirection.Output);

        await Assert.That(channel.Direction).IsEqualTo(ChannelDirection.Output);
    }

    [Test]
    public async Task WriteAsync_WritesToChannel()
    {
        await using var io = CreateIO();
        var channel = io.CreateMemoryChannel("test");

        var result = await io.WriteAsync("test", new { Name = "value" });

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task WriteAsync_NonexistentChannel_ReturnsError()
    {
        await using var io = CreateIO();

        var result = await io.WriteAsync("nonexistent", "data");

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ChannelNotFound");
    }

    [Test]
    public async Task WriteAsync_ReadOnlyChannel_ReturnsError()
    {
        await using var io = CreateIO();
        var stream = new MemoryStream();
        io.Register(Channel.Input("test", stream));

        var result = await io.WriteAsync("test", "data");

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ChannelReadOnly");
    }

    [Test]
    public async Task ReadAsync_ReadsFromChannel()
    {
        await using var io = CreateIO();
        var channel = io.CreateMemoryChannel("test");
        await channel.WriteTextAsync("{\"Name\":\"John\"}");
        // Reset stream position for reading
        channel.Stream.Position = 0;

        var result = await io.ReadChannelAsync<TestData>("test");

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task ReadAsync_NonexistentChannel_ReturnsError()
    {
        await using var io = CreateIO();

        var result = await io.ReadChannelAsync<string>("nonexistent");

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ChannelNotFound");
    }

    [Test]
    public async Task ReadAsync_WriteOnlyChannel_ReturnsError()
    {
        await using var io = CreateIO();
        var stream = new MemoryStream();
        io.Register(Channel.Output("test", stream));

        var result = await io.ReadChannelAsync<string>("test");

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ChannelWriteOnly");
    }

    [Test]
    public async Task WriteTextAsync_WritesText()
    {
        await using var io = CreateIO();
        var channel = io.CreateMemoryChannel("test");

        var result = await io.WriteTextAsync("test", "hello world");

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task WriteTextAsync_NonexistentChannel_ReturnsError()
    {
        await using var io = CreateIO();

        var result = await io.WriteTextAsync("nonexistent", "data");

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ChannelNotFound");
    }

    [Test]
    public async Task ReadTextAsync_ReadsText()
    {
        await using var io = CreateIO();
        var channel = io.CreateMemoryChannel("test");
        await channel.WriteTextAsync("hello world");

        var result = await io.ReadTextAsync("test");

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("hello world");
    }

    [Test]
    public async Task ReadTextAsync_NonexistentChannel_ReturnsError()
    {
        await using var io = CreateIO();

        var result = await io.ReadTextAsync("nonexistent");

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ChannelNotFound");
    }

    [Test]
    public async Task DisposeAsync_DisposesAllChannels()
    {
        var io = CreateIO();
        var channel1 = io.CreateMemoryChannel("channel1");
        var channel2 = io.CreateMemoryChannel("channel2");

        await io.DisposeAsync();

        await Assert.That(channel1.IsOpen).IsFalse();
        await Assert.That(channel2.IsOpen).IsFalse();
    }

    [Test]
    public async Task DisposeAsync_ClearsChannelNames()
    {
        var io = CreateIO();
        io.CreateMemoryChannel("test");

        await io.DisposeAsync();

        await Assert.That(io.ChannelNames.Any()).IsFalse();
    }

    [Test]
    public async Task WriteAsync_WithCustomContentType_UsesContentType()
    {
        await using var io = CreateIO();
        var channel = io.CreateMemoryChannel("test");

        await io.WriteAsync("test", data: "hello", contentType: "text/plain");

        var text = await channel.ReadAllTextAsync();
        await Assert.That(text).IsEqualTo("hello" + Environment.NewLine);
    }

    [Test]
    public async Task WriteAsync_UsesChannelContentType()
    {
        await using var io = CreateIO();
        var channel = io.CreateMemoryChannel("test");
        channel.ContentType = "text/plain";

        await io.WriteAsync("test", "hello");

        var text = await channel.ReadAllTextAsync();
        await Assert.That(text).IsEqualTo("hello" + Environment.NewLine);
    }

    private class TestData
    {
        public string? Name { get; set; }
    }
}
