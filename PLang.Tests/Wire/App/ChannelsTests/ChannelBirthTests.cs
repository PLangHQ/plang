namespace PLang.Tests.App.ChannelsTests;

/// <summary>A channel's birth makes only what is asked for: a channel that only writes holds no read buffer.</summary>
public class ChannelBirthTests
{
    // the bytes one birth of what make makes allocates, the JIT and statics paid first
    private static long Bytes(System.Func<object> make)
    {
        make();
        var from = System.GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 20; i++) make();
        return (System.GC.GetAllocatedBytesForCurrentThread() - from) / 20;
    }

    // a channel is born of its actor's channels: its actor is theirs
    [Test]
    public async Task AChannelsActor_IsItsChannelsActor()
    {
        await using var app = new global::app.@this("/test", wiring: global::app.Wiring.none).Testing();
        var user = global::app.channel.type.stream.@this.Memory(app.actor.list.User.Channel, "probe");
        var system = global::app.channel.type.stream.@this.Memory(app.actor.list.System.Channel, "probe");

        await Assert.That(user.Actor).IsSameReferenceAs(app.actor.list.User);
        await Assert.That(system.Actor).IsSameReferenceAs(app.actor.list.System);
        await Assert.That(user.Channels).IsSameReferenceAs(app.actor.list.User.Channel);
    }

    // a channel is named only where it was born: one born of the user's channels is refused by the system's, which
    // keep what they had
    [Test]
    public async Task AChannelBornOfAnotherActorsChannels_IsRefused_AndTheListIsUnchanged()
    {
        await using var app = new global::app.@this("/test", wiring: global::app.Wiring.none).Testing();
        var system = app.actor.list.System.Channel;
        var before = system.Get(global::app.channel.list.@this.Output);
        var stray = global::app.channel.type.stream.@this.Memory(app.actor.list.User.Channel, global::app.channel.list.@this.Output);

        await Assert.That(() => system.Register(stray)).Throws<InvalidOperationException>();
        await Assert.That(async () => await system.Set(stray)).Throws<InvalidOperationException>();
        await Assert.That(system.Get(global::app.channel.list.@this.Output)).IsSameReferenceAs(before);
    }

    [Test]
    public async Task AStreamChannelThatNeverReads_HoldsNoReadBuffer()
    {
        await using var app = new global::app.@this("/test", wiring: global::app.Wiring.none).Testing();
        var born = Bytes(() => new global::app.channel.type.console.@this(app.actor.list.User.Channel, "output", System.IO.Stream.Null, global::app.channel.ChannelDirection.Output));

        await Assert.That(born).IsLessThan(32 * 1024L).Because("the 64 KB read-ahead is the first read's to make");
    }

    [Test]
    public async Task AChannelsSettings_AreMadeWhenRead()
    {
        await using var app = new global::app.@this("/test", wiring: global::app.Wiring.none).Testing();
        var born = Bytes(() => new global::app.channel.type.console.@this(app.actor.list.User.Channel, "output", System.IO.Stream.Null, global::app.channel.ChannelDirection.Output));

        await Assert.That(born).IsLessThan(4 * 1024L).Because("a channel's direction, buffer and mime are made when read, unless given");
    }

    [Test]
    public async Task ASubAppThatNeverWrites_WiresNoChannel()
    {
        var born = Bytes(() =>
        {
            var app = new global::app.@this("/channel-birth", global::PLang.Tests.Parent.Actor);
            _ = app.DisposeAsync();
            return app;
        });

        await Assert.That(born).IsLessThan(220 * 1024L).Because("an actor's console channels are wired at its channels' first use");
    }

    [Test]
    public async Task ASubAppsChannels_AreWiredAtTheirFirstUse()
    {
        await using var app = new global::app.@this("/channel-birth", global::PLang.Tests.Parent.Actor);

        foreach (var name in global::app.channel.list.@this.Defaults)
            await Assert.That(app.actor.list.User.Channel.Get(name)).IsNotNull();
        await app.actor.list.System.Channel.Verify().IsSuccess();
    }

    [Test]
    public async Task AStreamChannel_ReadsAMessageLongerThanItsFirstBuffer()
    {
        await using var app = new global::app.@this("/channel-birth", global::PLang.Tests.Parent.Actor).Testing();
        var line = new string('x', 100_000);
        var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(line + "\nnext\n"));
        var channel = new global::app.channel.type.console.@this(app.actor.list.User.Channel, "long", stream, global::app.channel.ChannelDirection.Bidirectional);
        app.actor.list.User.Channel.Register(channel);

        var first = await channel.Read();
        var second = await channel.Read();

        await first.IsSuccess();
        await Assert.That((await first.Value())?.ToString()).IsEqualTo(line);
        await Assert.That((await second.Value())?.ToString()).IsEqualTo("next");
    }
}
