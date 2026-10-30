using PLangEngine = global::app.@this;
using data = global::app.data.@this;
using FileItem = global::app.type.item.file.@this;

namespace PLang.Tests.App.Modules.CatalogTests;

/// <summary>
/// The module knows its docs folder (<c>os/system/modules/&lt;module&gt;</c>); its description and its
/// actions' description/examples are lazy <c>file</c> handles in it (an action's notes are read line by line: its
/// <c>Note</c>). A handle is born unread: truthiness is EXISTENCE (so <c>{% if action.Examples %}</c> guards
/// presence without reading), and the content materializes only at the value door.
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
        _mdRoot = Path.Combine(_tempDir, "system", "modules");
        Directory.CreateDirectory(Path.Combine(_mdRoot, FixtureModule));

        _app = new global::app.@this(_tempDir).Testing();
        _app.module.Register(FixtureModule, FixtureAction1, typeof(FixtureAction));
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

    private FileItem Examples() => _app.Module(FixtureModule)![FixtureAction1]!.Examples;

    [Test]
    public async Task ModuleDescription_IsAFileHandle_InTheModuleFolder()
    {
        var handle = _app.Module(FixtureModule)!.Description;
        await Assert.That(handle).IsTypeOf<FileItem>();
        await Assert.That(handle.Path.Name.ToString()).IsEqualTo("module.description.md");
    }

    [Test]
    public async Task ActionExamples_IsAFileHandle_InItsModulesFolder()
    {
        await Assert.That(Examples().Path.Name.ToString()).IsEqualTo(FixtureAction1 + ".examples.md");
    }

    [Test]
    public async Task Prose_AbsentFile_IsFalsy_WithoutReading()
    {
        // No file staged — the handle exists but its location doesn't; truthiness is existence.
        await Assert.That(await Examples().AsBooleanAsync(_app.actor.list.System.Context!)).IsFalse();
        await Assert.That(Examples().IsLoaded).IsFalse();
    }

    [Test]
    public async Task Prose_StagedFile_IsTruthy_AndReadsContentAtTheValueDoor()
    {
        Stage(FixtureAction1 + ".examples.md", "Action rule.");

        var handle = Examples();
        await Assert.That(await handle.AsBooleanAsync(_app.actor.list.System.Context!)).IsTrue();   // existence, no content read yet
        await Assert.That(handle.IsLoaded).IsFalse();

        var content = await new data("prose", handle, context: _app.actor.list.System.Context).Value();
        await Assert.That(content?.ToString()).IsEqualTo("Action rule.");
    }

    [Test]
    public async Task Prose_Handles_CacheOnTheElement()
    {
        var module = _app.Module(FixtureModule)!;
        var action = module[FixtureAction1]!;
        await Assert.That(module.Description).IsSameReferenceAs(module.Description);
        await Assert.That(action.Examples).IsSameReferenceAs(action.Examples);
        await Assert.That(action.Note).IsSameReferenceAs(action.Note);
    }
}
