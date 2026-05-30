namespace PLang.Tests.App.ChannelsTests;

// Stage 5 (builder side) — channel.set / channel.remove surface in the catalog
// with full parameter descriptions. v3 collapsed channel.add into channel.set
// (always upserts), so add no longer exists.

public class Stage5_ChannelActionsBuilderCatalogTests
{
    [Test]
    public async Task BuilderCatalog_IncludesChannelSetAndRemove_WithParameters()
    {
        var app = new global::app.@this("/tmp/s5cat");
        var actions = await app.Module.Describe();

        var set = actions.FirstOrDefault(a => a.Module == "channel" && a.ActionName == "set");
        var remove = actions.FirstOrDefault(a => a.Module == "channel" && a.ActionName == "remove");
        var add = actions.FirstOrDefault(a => a.Module == "channel" && a.ActionName == "add");

        await Assert.That(set).IsNotNull();
        await Assert.That(remove).IsNotNull();
        // add was collapsed into set in v3
        await Assert.That(add).IsNull();

        // set: Name + Goal (+ optional Actor + config)
        await Assert.That(set!.Parameters.Any(p => p.Name == "Name")).IsTrue();
        await Assert.That(set.Parameters.Any(p => p.Name == "Goal")).IsTrue();
        await Assert.That(set.Parameters.Any(p => p.Name == "Buffer")).IsTrue();
        await Assert.That(set.Parameters.Any(p => p.Name == "Timeout")).IsTrue();
        await Assert.That(set.Parameters.Any(p => p.Name == "Mime")).IsTrue();
        await Assert.That(set.Parameters.Any(p => p.Name == "Encoding")).IsTrue();

        // remove: Name (+ optional Actor)
        await Assert.That(remove!.Parameters.Any(p => p.Name == "Name")).IsTrue();
    }
}
