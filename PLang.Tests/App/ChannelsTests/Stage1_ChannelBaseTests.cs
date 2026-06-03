using System.Reflection;

namespace PLang.Tests.App.ChannelsTests;

// Channel base + Role + Config defaults.
// Architect: .bot/runtime2-channels/architect/stage-1-channel-base.md

public class Stage1_ChannelBaseTests
{
    [Test]
    public async Task ChannelBase_Properties_RoundTripOnConcreteSubtype()
    {
        var ch = new StreamChannel("custom", new MemoryStream(), ChannelDirection.Bidirectional, ownsStream: true)
        {
            Buffer = 8192L,
            Timeout = TimeSpan.FromSeconds(45),
            Mime = "application/json",
            Encoding = "utf-16",
            Encryption = "aes",
            Signing = "myKey"
        };
        await Assert.That(ch.Name).IsEqualTo("custom");
        await Assert.That(ch.Direction).IsEqualTo(ChannelDirection.Bidirectional);
        await Assert.That(ch.Buffer).IsEqualTo(8192L);
        await Assert.That(ch.Timeout).IsEqualTo(TimeSpan.FromSeconds(45));
        await Assert.That(ch.Mime).IsEqualTo("application/json");
        await Assert.That(ch.Encoding).IsEqualTo("utf-16");
        await Assert.That(ch.Encryption).IsEqualTo("aes");
        await Assert.That(ch.Signing).IsEqualTo("myKey");
    }

    [Test]
    public async Task ChannelBase_Defaults_MatchSpecTable()
    {
        var ch = StreamChannel.Memory("d");
        await Assert.That(ch.Buffer).IsEqualTo(4096L);
        await Assert.That(ch.Timeout).IsEqualTo(TimeSpan.FromSeconds(30));
        await Assert.That(ch.Mime).IsEqualTo("text/plain");
        await Assert.That(ch.Encoding).IsEqualTo("utf-8");
        await Assert.That(ch.Encryption).IsNull();
        await Assert.That(ch.Signing).IsEqualTo("auto");
    }

    [Test]
    public async Task ChannelBase_AbstractMethods_EnforcedBySubtype()
    {
        var baseType = typeof(Channel);
        await Assert.That(baseType.IsAbstract).IsTrue();
        var write = baseType.GetMethod("Write");
        var read = baseType.GetMethod("Read");
        var ask = baseType.GetMethod("Ask");
        await Assert.That(write).IsNotNull();
        await Assert.That(read).IsNotNull();
        await Assert.That(ask).IsNotNull();
        await Assert.That(write!.IsAbstract).IsTrue();
        await Assert.That(read!.IsAbstract).IsTrue();
        await Assert.That(ask!.IsAbstract).IsTrue();
    }

    [Test]
    public async Task Channels_DefaultsContains_OutputErrorInput()
    {
        await Assert.That(global::app.channel.list.@this.Defaults).Contains("output");
        await Assert.That(global::app.channel.list.@this.Defaults).Contains("error");
        await Assert.That(global::app.channel.list.@this.Defaults).Contains("input");
    }

    [Test]
    public async Task Encryption_DefaultsNull_SigningDefaultsAuto()
    {
        var ch = StreamChannel.Memory("d");
        await Assert.That(ch.Encryption).IsNull();
        await Assert.That(ch.Signing).IsEqualTo("auto");
    }

    [Test]
    public async Task ChannelBase_Buffer_IsLong_NotInt()
    {
        var bufferProp = typeof(Channel).GetProperty("Buffer", BindingFlags.Public | BindingFlags.Instance);
        await Assert.That(bufferProp).IsNotNull();
        await Assert.That(bufferProp!.PropertyType).IsEqualTo(typeof(long));
    }

    [Test]
    public async Task ChannelBase_Mime_DefaultDrivesSerializerSelection()
    {
        // Mime drives serializer selection. Default "text/plain" → text serializer.
        // Switching Mime to "application/json" → JSON serializer.
        await using var app = new global::app.@this("/test", autoWireConsoleChannels: false);

        var captureA = new MemoryStream();
        var jsonCh = new StreamChannel("json", captureA, ChannelDirection.Output, ownsStream: false)
        { Mime = "application/json" };
        app.User.Channel.Register(jsonCh);
        await jsonCh.Write(Data.Ok(new { name = "x" }));
        await jsonCh.DisposeAsync();
        var jsonText = global::System.Text.Encoding.UTF8.GetString(captureA.ToArray());
        await Assert.That(jsonText).Contains("\"name\"");

        var captureB = new MemoryStream();
        var textCh = new StreamChannel("txt", captureB, ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" };
        app.User.Channel.Register(textCh);
        await textCh.Write(Data.Ok("plain hello"));
        await textCh.DisposeAsync();
        var raw = global::System.Text.Encoding.UTF8.GetString(captureB.ToArray());
        await Assert.That(raw.Contains("plain hello")).IsTrue();
    }

    [Test]
    public async Task SessionVsMessage_AbstractsExist_WithDistinctSemantics()
    {
        var session = typeof(global::app.channel.type.session.@this);
        var message = typeof(global::app.channel.type.message.@this);
        await Assert.That(session.IsAbstract).IsTrue();
        await Assert.That(message.IsAbstract).IsTrue();
        await Assert.That(session.IsSubclassOf(typeof(Channel))).IsTrue();
        await Assert.That(message.IsSubclassOf(typeof(Channel))).IsTrue();
        await Assert.That(session).IsNotEqualTo(message);
    }
}
