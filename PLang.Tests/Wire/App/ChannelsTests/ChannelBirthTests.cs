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

    [Test]
    public async Task AStreamChannelThatNeverReads_HoldsNoReadBuffer()
    {
        var born = Bytes(() => new global::app.channel.type.console.@this("output", System.IO.Stream.Null, global::app.channel.ChannelDirection.Output));

        await Assert.That(born).IsLessThan(32 * 1024L).Because("the 64 KB read-ahead is the first read's to make");
    }

    [Test]
    public async Task AChannelsSettings_AreMadeWhenRead()
    {
        var born = Bytes(() => new global::app.channel.type.console.@this("output", System.IO.Stream.Null, global::app.channel.ChannelDirection.Output));

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
        var channel = new global::app.channel.type.console.@this("long", stream, global::app.channel.ChannelDirection.Bidirectional);
        app.actor.list.User.Channel.Register(channel);

        var first = await channel.Read();
        var second = await channel.Read();

        await first.IsSuccess();
        await Assert.That((await first.Value())?.ToString()).IsEqualTo(line);
        await Assert.That((await second.Value())?.ToString()).IsEqualTo("next");
    }
}
