namespace PLang.Tests.App.ChannelsTests;

// Stage 5 (builder side) — channel.set / channel.add / channel.remove all
// surface in the catalog with full parameter descriptions.
// Per-action behavioural tests live as PLang `.goal` tests under Tests/Channels/.

public class Stage5_ChannelActionsBuilderCatalogTests
{
    [Test]
    public async Task BuilderCatalog_IncludesAllThreeChannelActions_WithParameters()
    {
        // The Modules describe path emits channel.set, channel.add, channel.remove
        // with their full parameter shape (role/name, goal target, buffer, timeout,
        // mime, encoding, encryption, signing). buffer is int bytes; timeout is
        // ISO 8601 string; others are plain strings.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
