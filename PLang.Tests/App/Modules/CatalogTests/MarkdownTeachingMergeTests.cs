using app.modules;

namespace PLang.Tests.App.Modules.CatalogTests;

/// <summary>
/// Merge semantics:
///   - When both module.* and &lt;action&gt;.* exist → CONCAT, module-level first,
///     then action-specific, separated by a blank line.
///   - Override semantics are explicitly rejected by the design.
///
/// The catalog entry keeps the two layers split (see LoaderTests). These tests
/// pin the merge helper that the renderer calls to produce a single block.
/// </summary>
public class MarkdownTeachingMergeTests
{
    [Test]
    public async Task Merge_NotesBothLayersPresent_ConcatModuleThenActionWithBlankLine()
    {
        var merged = MarkdownTeaching.MergeLayers("Module rule.", "Action rule.");
        await Assert.That(merged).IsEqualTo("Module rule.\n\nAction rule.");
    }

    [Test]
    public async Task Merge_OnlyActionLayerPresent_RendersJustActionText()
    {
        var merged = MarkdownTeaching.MergeLayers(null, "Just action.");
        await Assert.That(merged).IsEqualTo("Just action.");
    }

    [Test]
    public async Task Merge_OnlyModuleLayerPresent_RendersJustModuleText()
    {
        var merged = MarkdownTeaching.MergeLayers("Just module.", null);
        await Assert.That(merged).IsEqualTo("Just module.");
    }

    [Test]
    public async Task Merge_BothEmptyOrMissing_NoBlockRendered()
    {
        // Both null → null (signals "omit block").
        await Assert.That(MarkdownTeaching.MergeLayers(null, null)).IsNull();
        // Both whitespace-only → also null. Loader strips empties, but the
        // helper should be defensive (the architect's "Empty / missing files
        // are fine" rule applies symmetrically to either layer).
        await Assert.That(MarkdownTeaching.MergeLayers("   ", "\n\n")).IsNull();
    }
}
