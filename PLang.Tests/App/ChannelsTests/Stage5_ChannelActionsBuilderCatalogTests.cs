namespace PLang.Tests.App.ChannelsTests;

// Stage 5 (builder side) — channel.set / channel.add / channel.remove all
// surface in the catalog with full parameter descriptions.

public class Stage5_ChannelActionsBuilderCatalogTests
{
    [Test]
    public async Task BuilderCatalog_IncludesAllThreeChannelActions_WithParameters()
    {
        var app = new global::App.@this("/tmp/s5cat");
        var actions = app.Modules.Describe();

        var set = actions.FirstOrDefault(a => a.Module == "channel" && a.ActionName == "set");
        var add = actions.FirstOrDefault(a => a.Module == "channel" && a.ActionName == "add");
        var remove = actions.FirstOrDefault(a => a.Module == "channel" && a.ActionName == "remove");

        await Assert.That(set).IsNotNull();
        await Assert.That(add).IsNotNull();
        await Assert.That(remove).IsNotNull();

        // set: Role + Goal (+ optional Actor)
        await Assert.That(set!.Parameters.Any(p => p.Name == "Role")).IsTrue();
        await Assert.That(set.Parameters.Any(p => p.Name == "Goal")).IsTrue();

        // add: Name + Goal + config (Buffer / Timeout / Mime / Encoding)
        await Assert.That(add!.Parameters.Any(p => p.Name == "Name")).IsTrue();
        await Assert.That(add.Parameters.Any(p => p.Name == "Goal")).IsTrue();
        await Assert.That(add.Parameters.Any(p => p.Name == "Buffer")).IsTrue();
        await Assert.That(add.Parameters.Any(p => p.Name == "Timeout")).IsTrue();
        await Assert.That(add.Parameters.Any(p => p.Name == "Mime")).IsTrue();
        await Assert.That(add.Parameters.Any(p => p.Name == "Encoding")).IsTrue();

        // remove: Name (+ optional Actor)
        await Assert.That(remove!.Parameters.Any(p => p.Name == "Name")).IsTrue();
    }
}
