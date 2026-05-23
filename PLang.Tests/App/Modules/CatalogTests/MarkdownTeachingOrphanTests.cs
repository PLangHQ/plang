using AppModules = global::app.modules.@this;

namespace PLang.Tests.App.Modules.CatalogTests;

/// <summary>
/// Orphan-file validation (per architect plan, "Validation" section):
///   - At catalog load, any *.notes.md / *.examples.md / *.description.md under
///     os/system/modules/&lt;module&gt;/ whose stem is not `module` and does not
///     match a registered action → ONE warning per orphan.
///   - Warnings go through the warning channel (NOT Console.*) — see
///     project CLAUDE.md "No Console.* writes in production C#".
///   - Orphans MUST NOT crash the build. Catalog still assembles.
///
/// This replaces the "compiler catches typos in attribute argument strings" hole.
/// </summary>
public class MarkdownTeachingOrphanTests
{
    [Test]
    public async Task OrphanFile_UnknownActionNotesMd_WarnsViaWarningChannelAndContinues()
    {
        // Module folder has `unknownaction.notes.md` but no `unknownaction` is
        // registered. Loader emits exactly one warning (text identifies the
        // orphan path), build does not throw, catalog still contains the
        // module's real actions.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task OrphanFile_ModuleStemIsNeverOrphanEvenWithoutModuleAction()
    {
        // No action literally named "module" exists (it's a reserved stem per
        // architect plan). `module.notes.md` must NEVER be reported as orphan,
        // regardless of which actions are registered in the module.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task OrphanFile_MultipleOrphans_WarnsOncePerOrphan()
    {
        // Two orphan files in the same folder → exactly two warnings, one per
        // orphan path. No deduping that would hide real typos.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
