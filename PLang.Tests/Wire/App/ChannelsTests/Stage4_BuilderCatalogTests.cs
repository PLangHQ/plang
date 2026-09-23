namespace PLang.Tests.App.ChannelsTests;

// Stage 4 (builder side) — catalog teaches LLM the Channel parameter on
// IChannel actions and passes per-actor channel inventory at build time.
// Architect plan.md "Builder impact": intent-over-patterns.

public class Stage4_BuilderCatalogTests
{
    [Test]
    public async Task BuilderCatalog_DescribesChannelParameter_OnIChannelActions()
    {
        var app = global::PLang.Tests.TestApp.Create("/tmp/s4cat-a");
        var write = app.Module["output"]["write"];
        await Assert.That(write).IsNotNull();
        await Assert.That(write!.Property.Rows.Any(r => r.Name == "channel")).IsTrue();
    }

    [Test]
    public async Task BuilderCatalog_PassesPerActorChannelInventory_AtBuildTime()
    {
        var app = global::PLang.Tests.TestApp.Create("/tmp/s4cat-b");
        global::app.@this.WireDefaultConsoleChannels(app.User);
        app.User.Channel.Register(StreamChannel.Memory("logger"));

        var inventory = app.User.Channel.ChannelNames.ToList();
        await Assert.That(inventory).Contains("output");
        await Assert.That(inventory).Contains("error");
        await Assert.That(inventory).Contains("input");
        await Assert.That(inventory).Contains("logger");
    }
}
