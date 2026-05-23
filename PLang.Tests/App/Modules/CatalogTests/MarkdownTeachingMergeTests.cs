using AppModules = global::app.modules.@this;

namespace PLang.Tests.App.Modules.CatalogTests;

/// <summary>
/// Merge semantics (per architect plan, "Merge semantics" section):
///   - When both module.* and &lt;action&gt;.* exist → CONCAT, module-level first,
///     then action-specific, separated by a blank line.
///   - Override semantics are explicitly rejected by the design.
///
/// The catalog entry keeps the two layers split (see LoaderTests). These tests
/// pin the rendered/concatenated form — whichever surface the coder picks for
/// "the text the renderer feeds the prompt." Either a helper on the catalog
/// entry or the template merging directly; test against the helper if one exists,
/// otherwise against renderer output (covered separately in StepActionDetails-
/// RenderTests).
/// </summary>
public class MarkdownTeachingMergeTests
{
    [Test]
    public async Task Merge_NotesBothLayersPresent_ConcatModuleThenActionWithBlankLine()
    {
        // module.notes.md = "Module rule." ; setvalue.notes.md = "Action rule."
        // Merged Notes block (renderer-facing) = "Module rule.\n\nAction rule."
        // Module text FIRST, blank line, action text. No reordering.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Merge_OnlyActionLayerPresent_RendersJustActionText()
    {
        // module.notes.md missing, setvalue.notes.md = "Just action."
        // Merged block = "Just action." — no leading blank line, no separator.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Merge_OnlyModuleLayerPresent_RendersJustModuleText()
    {
        // setvalue.notes.md missing, module.notes.md = "Just module."
        // Merged block = "Just module." — no trailing blank line.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Merge_BothEmptyOrMissing_NoBlockRendered()
    {
        // Neither file present (or both present but empty after trim). The
        // renderer must omit the Notes block entirely — not "Notes:\n" with
        // empty body. The architect's "Empty / missing files are fine" rule.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
