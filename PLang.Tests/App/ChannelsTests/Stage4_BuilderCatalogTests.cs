namespace PLang.Tests.App.ChannelsTests;

// Stage 4 (builder side) — catalog teaches LLM the Channel parameter on
// IChannel actions and passes per-actor channel inventory at build time.
// Architect plan.md "Builder impact": intent-over-patterns.

public class Stage4_BuilderCatalogTests
{
    [Test]
    public async Task BuilderCatalog_DescribesChannelParameter_OnIChannelActions()
    {
        var app = new global::app.@this("/tmp/s4cat-a");
        var actions = await app.Module.Describe();
        var write = actions.FirstOrDefault(a => a.Module == "output" && a.ActionName == "write");
        await Assert.That(write).IsNotNull();
        var channelParam = write!.Parameters.FirstOrDefault(p => p.Name == "channel");
        await Assert.That(channelParam).IsNotNull();
    }

    [Test]
    public async Task BuilderCatalog_PassesPerActorChannelInventory_AtBuildTime()
    {
        var app = new global::app.@this("/tmp/s4cat-b");
        global::app.@this.WireDefaultConsoleChannels(app.User);
        app.User.Channel.Register(StreamChannel.Memory("logger"));

        var inventory = app.Module.GetChannelInventory(app.User);
        await Assert.That(inventory).Contains("output");
        await Assert.That(inventory).Contains("error");
        await Assert.That(inventory).Contains("input");
        await Assert.That(inventory).Contains("logger");
    }

    [Test]
    public async Task BuilderCatalog_MapsIntentToChannelName_NotPatternParse()
    {
        // Intent-over-pattern is enforced by what the catalog sends to the LLM:
        // the parameter is `channel: string?` (no `to <name>` regex), and the
        // inventory lists registered names. Real-LLM verification is integration-
        // level. Here we verify the structural pre-condition: no syntactic-pattern
        // hint appears in the channel parameter description.
        var app = new global::app.@this("/tmp/s4cat-c");
        var actions = await app.Module.Describe();
        var write = actions.First(a => a.Module == "output" && a.ActionName == "write");
        var channelParam = write.Parameters.First(p => p.Name == "channel");
        var desc = (await channelParam.Value()) as string ?? "";
        await Assert.That(desc.Contains("to ")).IsFalse();
        await Assert.That(desc.Contains("pattern")).IsFalse();
    }
}
