using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.CatalogTests;

/// <summary>
/// Catalog loader reads per-action LLM teaching from markdown files at
/// <c>os/system/modules/&lt;module&gt;/{module,&lt;action&gt;}.{notes,examples,description}.md</c>
/// and surfaces both layers (module-level + action-level) on the catalog entry.
/// Renderer concats at render time — these tests pin that the LOADER keeps the
/// two layers separate and visible.
///
/// Fixture strategy: spin a fresh engine over a temp working directory, then
/// register a fake action on a fake module namespace and stage the markdown
/// fixtures in a temp <c>MarkdownTeachingRoot</c>. Tests assert on the resulting
/// catalog entry.
/// </summary>
public class MarkdownTeachingLoaderTests
{
    /// <summary>Stand-in parameter schema for a registered fixture action. No properties — Describe just needs a Type to reflect over.</summary>
    private sealed class FixtureAction { }
    private sealed class FixtureAction2 { }

    private const string FixtureModule = "fixturemod";
    private const string FixtureAction1 = "setvalue";
    private const string FixtureActionB = "getvalue";

    private string _tempDir = null!;
    private string _mdRoot = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "plang_mdteach_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _mdRoot = Path.Combine(_tempDir, "mdroot");
        Directory.CreateDirectory(Path.Combine(_mdRoot, FixtureModule));

        _app = new PLangEngine(_tempDir);
        _app.Modules.MarkdownTeachingRoot = _mdRoot;
        _app.Modules.RegisterType(FixtureModule, FixtureAction1, typeof(FixtureAction));
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try
        {
            await _app.DisposeAsync();
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }
        catch { /* best effort */ }
    }

    private void Stage(string fileName, string body)
        => File.WriteAllText(Path.Combine(_mdRoot, FixtureModule, fileName), body);

    private async Task<global::app.goal.steps.step.actions.action.@this> Find(string action)
    {
        var catalog = await _app.Modules.Describe();
        var row = catalog.FirstOrDefault(a => a.Module == FixtureModule && a.ActionName == action);
        if (row == null)
            throw new InvalidOperationException($"catalog missing {FixtureModule}.{action}");
        return row;
    }

    [Test]
    public async Task Describe_ActionNotesMarkdownPresent_PopulatesActionNotes()
    {
        Stage($"{FixtureAction1}.notes.md", "Action-only note.");
        var row = await Find(FixtureAction1);
        await Assert.That(row.Notes).IsEqualTo("Action-only note.");
        await Assert.That(row.ModuleNotes).IsNull();
    }

    [Test]
    public async Task Describe_ModuleNotesMarkdownPresent_PopulatesModuleNotes()
    {
        Stage("module.notes.md", "Module-wide rule.");
        _app.Modules.RegisterType(FixtureModule, FixtureActionB, typeof(FixtureAction2));

        var a = await Find(FixtureAction1);
        var b = await Find(FixtureActionB);
        await Assert.That(a.ModuleNotes).IsEqualTo("Module-wide rule.");
        await Assert.That(b.ModuleNotes).IsEqualTo("Module-wide rule.");
        await Assert.That(a.Notes).IsNull();
        await Assert.That(b.Notes).IsNull();
    }

    [Test]
    public async Task Describe_BothLayersPresent_KeepsThemSplit()
    {
        Stage("module.notes.md", "Module rule.");
        Stage($"{FixtureAction1}.notes.md", "Action rule.");

        var row = await Find(FixtureAction1);
        // Layers stay split — renderer concats, loader does not.
        await Assert.That(row.ModuleNotes).IsEqualTo("Module rule.");
        await Assert.That(row.Notes).IsEqualTo("Action rule.");
    }

    [Test]
    public async Task Describe_DescriptionMarkdownPresent_PopulatesActionAndModuleDescription()
    {
        Stage("module.description.md", "Fixturemod handles fake things.");
        Stage($"{FixtureAction1}.description.md", "Sets a fixture value.");

        var row = await Find(FixtureAction1);
        await Assert.That(row.Description).IsEqualTo("Sets a fixture value.");
        await Assert.That(row.ModuleDescription).IsEqualTo("Fixturemod handles fake things.");
    }

    [Test]
    public async Task Describe_ExamplesMarkdownPresent_PopulatesExamplesList()
    {
        Stage($"{FixtureAction1}.examples.md",
            "First example paragraph.\n\nSecond example paragraph.");
        var row = await Find(FixtureAction1);
        await Assert.That(row.ExamplesMd.Count).IsEqualTo(2);
        await Assert.That(row.ExamplesMd[0]).IsEqualTo("First example paragraph.");
        await Assert.That(row.ExamplesMd[1]).IsEqualTo("Second example paragraph.");
    }

    [Test]
    public async Task Describe_NoMarkdownFilesAtAll_LeavesAllTeachingFieldsNullOrEmpty()
    {
        var row = await Find(FixtureAction1);
        await Assert.That(row.Notes).IsNull();
        await Assert.That(row.ModuleNotes).IsNull();
        // Description / ModuleDescription remain null when no [Description] attribute and no markdown.
        await Assert.That(row.Description).IsNull();
        await Assert.That(row.ModuleDescription).IsNull();
        await Assert.That(row.ExamplesMd.Count).IsEqualTo(0);
        await Assert.That(row.ModuleExamplesMd.Count).IsEqualTo(0);
    }
}
