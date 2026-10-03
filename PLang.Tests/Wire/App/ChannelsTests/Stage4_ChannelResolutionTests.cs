using app.channel;

namespace PLang.Tests.App.ChannelsTests;

// Channel slot resolution + IChannel marker + Write.Start.
// Architect: stage-4-write-channel-slot.md.

public class Stage4_ChannelResolutionTests
{
    [Test]
    public async Task SourceGen_EmitsChannelResolutionCode_ForIChannelActions()
    {
        // The Write action implements IChannel — the generator emits a
        // `Channel { get; set; }` slot. Verify via reflection.
        var writeType = typeof(global::app.module.output.Write);
        var prop = writeType.GetProperty("Channel");
        await Assert.That(prop).IsNotNull();
        await Assert.That(prop!.PropertyType).IsEqualTo(typeof(global::app.channel.@this));
    }

    [Test]
    public async Task ChannelsGet_Output_ReturnsChannelNamedOutput()
    {
        var app = new global::app.@this("/tmp/s4a").Testing();
        global::app.@this.WireDefaultConsoleChannels(app.actor.list.User);
        var ch = app.actor.list.User.Channel.Get(global::app.channel.list.@this.Output);
        await Assert.That(ch).IsNotNull();
        await Assert.That(ch!.Name).IsEqualTo("output");
    }

    [Test]
    public async Task ChannelsGet_NamedChannel_ReturnsThatChannel()
    {
        var app = new global::app.@this("/tmp/s4b").Testing();
        var logger = StreamChannel.Memory("logger");
        app.actor.list.User.Channel.Register(logger);
        var ch = app.actor.list.User.Channel.Get("logger");
        await Assert.That((Channel?)ch).IsEqualTo((Channel)logger);
    }

    [Test]
    public async Task ChannelsGet_UnknownName_ReturnsNull()
    {
        var app = new global::app.@this("/tmp/s4c").Testing();
        var ch = app.actor.list.User.Channel.Get("dbg");
        await Assert.That(ch).IsNull();
    }

    [Test]
    public async Task WriteRun_NoChannelSlot_WritesToDefaultOutput()
    {
        var app = new global::app.@this("/tmp/s4d").Testing();
        var captured = new MemoryStream();
        app.actor.list.User.Channel.Register(new StreamChannel("output", captured, ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" });

        var write = new global::app.module.output.Write(app.actor.list.User.Context) { Data = app.Ok("hello-default"),
            Channel = app.actor.list.User.Channel.Get(global::app.channel.list.@this.Output)
        };
        // Direct Start skips the dispatcher's reset of init backing fields.
        await write.Start();

        var bytes = global::System.Text.Encoding.UTF8.GetString(captured.ToArray());
        await Assert.That(bytes.Contains("hello-default")).IsTrue();
    }

    [Test]
    public async Task WriteRun_WithChannelSlot_WritesToThatChannel()
    {
        var app = new global::app.@this("/tmp/s4e").Testing();
        var loggerCapture = new MemoryStream();
        app.actor.list.User.Channel.Register(new StreamChannel("logger", loggerCapture, ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" });

        var write = new global::app.module.output.Write(app.actor.list.User.Context) { Data = app.Ok("targetted"),
            Channel = app.actor.list.User.Channel.Get("logger")
        };
        await write.Start();

        var bytes = global::System.Text.Encoding.UTF8.GetString(loggerCapture.ToArray());
        await Assert.That(bytes.Contains("targetted")).IsTrue();
    }

    [Test]
    public async Task Write_PassesFullDataEnvelope_NotJustValue()
    {
        // Plan rule 7: relay don't repackage. Channel.WriteAsync receives full Data.
        var app = new global::app.@this("/tmp/s4f").Testing();
        var probe = new EnvelopeProbeChannel();
        app.actor.list.User.Channel.Register(probe);

        var data = app.Ok("payload");
        data.Property.Set("custom-prop", "x");

        var write = new global::app.module.output.Write(app.actor.list.User.Context) { Data = data,
            Channel = probe
        };
        await write.Start();

        await Assert.That(probe.Received).IsNotNull();
        await Assert.That(ReferenceEquals(probe.Received, data)).IsTrue();
    }

    [Test]
    public async Task ChannelsThis_WriteAsyncWriteOverload_IsRemoved()
    {
        var channelsType = typeof(global::app.channel.list.@this);
        var writeOverload = channelsType.GetMethods()
            .FirstOrDefault(m => m.Name == "WriteAsync"
                && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType == typeof(global::app.module.output.Write));
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
        public override Task<Data> Ask(global::app.module.output.ask action, CancellationToken ct = default) => Task.FromResult(Data.Ok());
    }
}
