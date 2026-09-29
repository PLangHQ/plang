namespace PLang.Tests.App.ChannelsTests;

// Stage 4 (builder side) — catalog teaches LLM the Channel parameter on
// IChannel actions and passes per-actor channel inventory at build time.
// Architect plan.md "Builder impact": intent-over-patterns.

public class Stage4_BuilderCatalogTests
{
    [Test]
    public async Task BuilderCatalog_DescribesChannelParameter_OnIChannelActions()
    {
        var app = new global::app.@this("/tmp/s4cat-a").Testing();
        var write = app.Module("output")["write"];
        await Assert.That(write).IsNotNull();
        await Assert.That(write!.Property.Any(r => r.Name == "channel")).IsTrue();
    }

    [Test]
    public async Task BuilderCatalog_PassesPerActorChannelInventory_AtBuildTime()
    {
        var app = new global::app.@this("/tmp/s4cat-b").Testing();
        global::app.@this.WireDefaultConsoleChannels(app.actor.list.User);
        app.actor.list.User.Channel.Register(StreamChannel.Memory("logger"));

        var inventory = app.actor.list.User.Channel.ChannelNames.ToList();
        await Assert.That(inventory).Contains("output");
        await Assert.That(inventory).Contains("error");
        await Assert.That(inventory).Contains("input");
        await Assert.That(inventory).Contains("logger");
    }
}
