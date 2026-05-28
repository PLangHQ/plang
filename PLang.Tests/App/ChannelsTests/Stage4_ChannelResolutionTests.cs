using app.channels;

namespace PLang.Tests.App.ChannelsTests;

// Channel slot resolution + IChannel marker + Write.Run.
// Architect: stage-4-write-channel-slot.md.

public class Stage4_ChannelResolutionTests
{
    [Test]
    public async Task SourceGen_EmitsChannelResolutionCode_ForIChannelActions()
    {
        // The Write action implements IChannel — the generator emits a
        // `Channel { get; set; }` slot. Verify via reflection.
        var writeType = typeof(global::app.modules.output.Write);
        var prop = writeType.GetProperty("Channel");
        await Assert.That(prop).IsNotNull();
        await Assert.That(prop!.PropertyType).IsEqualTo(typeof(global::app.channels.channel.@this));
    }

    [Test]
    public async Task ChannelsResolve_NullName_ReturnsChannelNamedOutput()
    {
        var app = new global::app.@this("/tmp/s4a");
        global::app.@this.WireDefaultConsoleChannels(app.User);
        var ch = app.User.Channels.Resolve(null);
        await Assert.That(ch).IsNotNull();
        await Assert.That(ch!.Name).IsEqualTo("output");
    }

    [Test]
    public async Task ChannelsResolve_NamedChannel_ReturnsThatChannel()
    {
        var app = new global::app.@this("/tmp/s4b");
        var logger = StreamChannel.Memory("logger");
        app.User.Channels.Register(logger);
        var ch = app.User.Channels.Resolve("logger");
        await Assert.That(ch).IsEqualTo((Channel)logger);
    }

    [Test]
    public async Task ChannelsResolve_UnknownName_ReturnsNull()
    {
        var app = new global::app.@this("/tmp/s4c");
        var ch = app.User.Channels.Resolve("dbg");
        await Assert.That(ch).IsNull();
    }

    [Test]
    public async Task WriteRun_NoChannelSlot_WritesToDefaultOutput()
    {
        var app = new global::app.@this("/tmp/s4d");
        var captured = new MemoryStream();
        app.User.Channels.Register(new StreamChannel("output", captured, ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" });

        var write = new global::app.modules.output.Write
        {
            Context = app.User.Context,
            Data = Data.Ok("hello-default"),
            Channel = app.User.Channels.Resolve(null)
        };
        // Direct Run skips ExecuteAsync's reset of init backing fields.
        await write.Run();

        var bytes = global::System.Text.Encoding.UTF8.GetString(captured.ToArray());
        await Assert.That(bytes.Contains("hello-default")).IsTrue();
    }

    [Test]
    public async Task WriteRun_WithChannelSlot_WritesToThatChannel()
    {
        var app = new global::app.@this("/tmp/s4e");
        var loggerCapture = new MemoryStream();
        app.User.Channels.Register(new StreamChannel("logger", loggerCapture, ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" });

        var write = new global::app.modules.output.Write
        {
            Context = app.User.Context,
            Data = Data.Ok("targetted"),
            Channel = app.User.Channels.Resolve("logger")
        };
        await write.Run();

        var bytes = global::System.Text.Encoding.UTF8.GetString(loggerCapture.ToArray());
        await Assert.That(bytes.Contains("targetted")).IsTrue();
    }

    [Test]
    public async Task Write_PassesFullDataEnvelope_NotJustValue()
    {
        // Plan rule 7: relay don't repackage. Channel.WriteAsync receives full Data.
        var app = new global::app.@this("/tmp/s4f");
        var probe = new EnvelopeProbeChannel();
        app.User.Channels.Register(probe);

        var data = Data.Ok("payload");
        data.Properties.Set("custom-prop", "x");

        var write = new global::app.modules.output.Write
        {
            Context = app.User.Context,
            Data = data,
            Channel = probe
        };
        await write.Run();

        await Assert.That(probe.Received).IsNotNull();
        await Assert.That(ReferenceEquals(probe.Received, data)).IsTrue();
    }

    [Test]
    public async Task ChannelsThis_WriteAsyncWriteOverload_IsRemoved()
    {
        var channelsType = typeof(EngineChannels);
        var writeOverload = channelsType.GetMethods()
            .FirstOrDefault(m => m.Name == "WriteAsync"
                && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType == typeof(global::app.modules.output.Write));
        await Assert.That(writeOverload).IsNull();
    }

    private sealed class EnvelopeProbeChannel : Channel
    {
        public Data? Received { get; private set; }
        public EnvelopeProbeChannel()
        {
            Name = "probe";
            Direction = ChannelDirection.Output;
        }
        public override Task<Data> Write(Data data, CancellationToken ct = default)
        {
            Received = data;
            return Task.FromResult(Data.Ok());
        }
        public override Task<Data> Read(CancellationToken ct = default) => Task.FromResult(Data.Ok());
        public override Task<Data> Ask(global::app.modules.output.ask action, CancellationToken ct = default) => Task.FromResult(Data.Ok());
    }
}
