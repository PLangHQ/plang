namespace PLang.Tests.App.ChannelsTests;

// Stage 4 (builder side) — catalog teaches LLM the Channel parameter on
// IChannel actions and passes per-actor channel inventory at build time.
// Architect plan.md "Builder impact": intent-over-patterns.

public class Stage4_BuilderCatalogTests
{
    [Test]
    public async Task BuilderCatalog_DescribesChannelParameter_OnIChannelActions()
    {
        // The Modules describe path emits a `channel` property entry for every
        // action implementing IChannel (Write being the canonical case).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task BuilderCatalog_PassesPerActorChannelInventory_AtBuildTime()
    {
        // For every IChannel action shown to the LLM, the catalog includes the
        // current actor's registered channel names. Mid-goal `add channel`
        // earlier in the same goal must show up in inventory for later steps.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task BuilderCatalog_MapsIntentToChannelName_NotPatternParse()
    {
        // Builder MUST NOT pattern-parse `to <name>` itself. The LLM picks
        // from inventory based on intent. Unit-level: feed the catalog a step
        // with arbitrary phrasing ("write 'hi' at the best logger ever") and
        // an inventory containing "logger" — verify the LLM's emitted JSON
        // has `"channel":"logger"`.
        // (This is a builder-integration test; uses a real LLM per the
        // test_design_principles convention for builder behaviour.)
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
