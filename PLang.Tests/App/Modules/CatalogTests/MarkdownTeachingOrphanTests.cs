using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.CatalogTests;

/// <summary>
/// Orphan-file validation:
///   - Any *.notes.md / *.examples.md / *.description.md under
///     os/system/modules/&lt;module&gt;/ whose stem is not `module` and does not
///     match a registered action → ONE warning per orphan.
///   - Warnings go through the actor's Output channel — see project CLAUDE.md
///     "No Console.* writes in production C#"; architect's coder plan pins
///     WriteTextAsync(Output, …) as the surface.
///   - Orphans MUST NOT crash the build. Catalog still assembles.
///
/// This replaces the "compiler catches typos in attribute argument strings" hole.
/// </summary>
public class MarkdownTeachingOrphanTests
{
    private sealed class FixtureAction { }

    private string _tempDir = null!;
    private string _mdRoot = null!;
    private PLangEngine _app = null!;
    private MemoryStream _capture = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "plang_orphan_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _mdRoot = Path.Combine(_tempDir, "mdroot");
        Directory.CreateDirectory(Path.Combine(_mdRoot, "fixturemod"));

        _app = new PLangEngine(_tempDir);
        _app.Modules.MarkdownTeachingRoot = _mdRoot;
        _app.Modules.RegisterType("fixturemod", "setvalue", typeof(FixtureAction));

        _capture = new MemoryStream();
        _app.User.Channels.Register(new StreamChannel(
            EngineChannels.Output, _capture,
            ChannelDirection.Output, ownsStream: false));
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try
        {
            await _app.DisposeAsync();
            _capture.Dispose();
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }
        catch { /* best effort */ }
    }

    private string CapturedOutput()
    {
        _capture.Position = 0;
        return new StreamReader(_capture).ReadToEnd();
    }

    private void Stage(string fileName, string body = "x")
        => File.WriteAllText(Path.Combine(_mdRoot, "fixturemod", fileName), body);

    [Test]
    public async Task OrphanFile_UnknownActionNotesMd_WarnsViaWarningChannelAndContinues()
    {
        Stage("unknownaction.notes.md");

        var orphans = await _app.Modules.WarnOrphansAsync(_app.User);
        await Assert.That(orphans.Count).IsEqualTo(1);
        await Assert.That(orphans[0].Stem).IsEqualTo("unknownaction");

        var output = CapturedOutput();
        await Assert.That(output).Contains("unknownaction.notes.md");

        // Catalog still assembles — the registered fixture action is still there.
        var catalog = await _app.Modules.Describe();
        await Assert.That(catalog.Any(a => a.Module == "fixturemod" && a.ActionName == "setvalue")).IsTrue();
    }

    [Test]
    public async Task OrphanFile_ModuleStemIsNeverOrphanEvenWithoutModuleAction()
    {
        Stage("module.notes.md");
        Stage("module.examples.md");
        Stage("module.description.md");

        var orphans = await _app.Modules.WarnOrphansAsync(_app.User);
        await Assert.That(orphans.Count).IsEqualTo(0);
        await Assert.That(CapturedOutput()).IsEmpty();
    }

    [Test]
    public async Task OrphanFile_MultipleOrphans_WarnsOncePerOrphan()
    {
        Stage("orphanA.notes.md");
        Stage("orphanB.description.md");

        var orphans = await _app.Modules.WarnOrphansAsync(_app.User);
        await Assert.That(orphans.Count).IsEqualTo(2);

        var output = CapturedOutput();
        await Assert.That(output).Contains("orphanA.notes.md");
        await Assert.That(output).Contains("orphanB.description.md");
    }
}
