using PLangEngine = global::app.@this;
using data = global::app.data.@this;
using FileItem = global::app.type.item.file.@this;

namespace PLang.Tests.App.Modules.CatalogTests;

/// <summary>
/// The module element exposes its teaching prose as lazy <c>file</c> handles over
/// <c>os/system/modules/&lt;module&gt;/module.{description,notes,examples}.md</c>.
/// A handle is born unread: truthiness is EXISTENCE (so <c>{% if module.Notes %}</c>
/// guards presence without reading), and the content materializes only at the value
/// door. This is the navigation face that replaces the eager <c>Describe()</c> load.
/// </summary>
public class ModuleProseDoorTests
{
    private sealed class FixtureAction { }

    private const string FixtureModule = "fixturemod";
    private const string FixtureAction1 = "setvalue";

    private string _tempDir = null!;
    private string _mdRoot = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "plang_prosedoor_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _mdRoot = Path.Combine(_tempDir, "mdroot");
        Directory.CreateDirectory(Path.Combine(_mdRoot, FixtureModule));

        _app = TestApp.Create(_tempDir);
        _app.Module.MarkdownTeachingRoot = _mdRoot;
        _app.Module.RegisterType(FixtureModule, FixtureAction1, typeof(FixtureAction));
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

    private FileItem Notes() => _app.Module[FixtureModule]!.Notes;

    [Test]
    public async Task Prose_IsAFileHandle_OverTheModuleFacet()
    {
        var handle = _app.Module[FixtureModule]!.Description;
        await Assert.That(handle).IsTypeOf<FileItem>();
        await Assert.That(handle.Path.FileName).IsEqualTo("module.description.md");
    }

    [Test]
    public async Task Prose_AbsentFile_IsFalsy_WithoutReading()
    {
        // No file staged — the handle exists but its location doesn't; truthiness is existence.
        await Assert.That(Notes().IsTruthy()).IsFalse();
        await Assert.That(Notes().IsLoaded).IsFalse();
    }

    [Test]
    public async Task Prose_StagedFile_IsTruthy_AndReadsContentAtTheValueDoor()
    {
        Stage("module.notes.md", "Module-wide rule.");

        var handle = Notes();
        await Assert.That(handle.IsTruthy()).IsTrue();   // existence, no content read yet
        await Assert.That(handle.IsLoaded).IsFalse();

        var content = await new data("prose", handle, context: _app.System.Context).Value();
        await Assert.That(content?.ToString()).IsEqualTo("Module-wide rule.");
    }

    [Test]
    public async Task Prose_Handles_CacheOnTheElement()
    {
        var element = _app.Module[FixtureModule]!;
        await Assert.That(element.Notes).IsSameReferenceAs(element.Notes);
    }
}
