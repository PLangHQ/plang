namespace PLang.Tests.App.ChannelsTests;

// Stage 5 (builder side) — channel.set / channel.remove surface in the catalog
// with full parameter descriptions. v3 collapsed channel.add into channel.set
// (always upserts), so add no longer exists.

public class Stage5_ChannelActionsBuilderCatalogTests
{
    [Test]
    public async Task BuilderCatalog_IncludesChannelSetAndRemove_WithParameters()
    {
        var app = global::PLang.Tests.TestApp.Create("/tmp/s5cat");
        var set = app.Module["channel"]["set"];
        var remove = app.Module["channel"]["remove"];
        var add = app.Module["channel"]["add"];

        await Assert.That(set).IsNotNull();
        await Assert.That(remove).IsNotNull();
        // add was collapsed into set
        await Assert.That(add).IsNull();

        // set: Name (+ optional Actor + config); the Goal slot is action-typed structure, not a row
        var setRows = set!.Property.Rows;
        await Assert.That(setRows.Any(r => r.Name == "Name")).IsTrue();
        await Assert.That(setRows.Any(r => r.Name == "Buffer")).IsTrue();
        await Assert.That(setRows.Any(r => r.Name == "Timeout")).IsTrue();
        await Assert.That(setRows.Any(r => r.Name == "Mime")).IsTrue();
        await Assert.That(setRows.Any(r => r.Name == "Encoding")).IsTrue();

        // remove: Name (+ optional Actor)
        await Assert.That(remove!.Property.Rows.Any(r => r.Name == "Name")).IsTrue();
    }
}
