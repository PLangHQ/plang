using AppModules = global::app.modules.@this;

namespace PLang.Tests.App.Modules.CatalogTests;

/// <summary>
/// Catalog loader reads per-action LLM teaching from markdown files at
/// <c>os/system/modules/&lt;module&gt;/{module,&lt;action&gt;}.{notes,examples,description}.md</c>
/// and surfaces both layers (module-level + action-level) on the catalog entry.
/// Renderer concats at render time — these tests pin that the LOADER keeps the
/// two layers separate and visible.
///
/// Fixture strategy: register a fake action on a fake module namespace, then
/// stage markdown files in the fixture filesystem under the matching path.
/// Coder picks the exact API for injecting the fixture root (probably
/// <c>App.FileSystem</c>). Tests assert on the resulting catalog entry.
/// </summary>
public class MarkdownTeachingLoaderTests
{
    [Test]
    public async Task Describe_ActionNotesMarkdownPresent_PopulatesActionNotes()
    {
        // Given module folder `os/system/modules/fixturemod/` contains
        // `setvalue.notes.md` with body "Action-only note." and no other files,
        // the catalog entry for fixturemod.setvalue carries Notes=that text and
        // ModuleNotes=null.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Describe_ModuleNotesMarkdownPresent_PopulatesModuleNotes()
    {
        // `module.notes.md` only; action carries ModuleNotes=text, Notes=null.
        // ModuleNotes must apply to every action in the module — assert a second
        // registered action under the same namespace sees the same ModuleNotes.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Describe_BothLayersPresent_KeepsThemSplit()
    {
        // Both `module.notes.md` and `setvalue.notes.md`. Catalog entry exposes
        // ModuleNotes=module text AND Notes=action text — NOT pre-concatenated.
        // Layers stay visible so renderer (and humans debugging) can see both.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Describe_DescriptionMarkdownPresent_PopulatesActionAndModuleDescription()
    {
        // `module.description.md` + `setvalue.description.md` → Description and
        // ModuleDescription fields populated from the files. After migration
        // these are the source of truth (the C# [Description] / [ModuleDescription]
        // attributes are gone from action classes).
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Describe_ExamplesMarkdownPresent_PopulatesExamplesList()
    {
        // `setvalue.examples.md` with two example paragraphs separated by a blank
        // line → Examples list has two entries in source order. Migration replaces
        // [Example] attributes; existing renderer-facing Examples shape preserved.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Describe_NoMarkdownFilesAtAll_LeavesAllTeachingFieldsNullOrEmpty()
    {
        // No markdown for the module folder → Notes=null, ModuleNotes=null,
        // Description=null (after migration), ModuleDescription=null,
        // Examples is empty list. The action is still in the catalog; only its
        // teaching is absent.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
