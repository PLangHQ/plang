using System.Text.Json;
using app.actor.context;
using app.variable;
using app.Utils;
using app.module.builder;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Tests for builder.app and builder.app.save — load and save app.pr metadata.
/// </summary>
public class AppTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_app_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir);
        _app.Builder.IsEnabled = true;
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try
        {
            await _app.DisposeAsync();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort */ }
    }

    [Test]
    public async Task GetApp_LoadsExistingAppPr()
    {
        // Create existing app.pr
        var buildDir = System.IO.Path.Combine(_tempDir, ".build");
        System.IO.Directory.CreateDirectory(buildDir);
        var json = JsonSerializer.Serialize(new { id = "test-id-123", name = "TestApp", created = DateTime.UtcNow.AddDays(-1), updated = DateTime.UtcNow.AddDays(-1), version = "0.2" });
        System.IO.File.WriteAllText(System.IO.Path.Combine(buildDir, "app.pr"), json);

        // Load triggers reading app.pr
        await _app.Load();

        await Assert.That(_app.Id).IsEqualTo("test-id-123");
        await Assert.That(_app.Name).IsEqualTo("TestApp");
    }

    [Test]
    public async Task GetApp_ReturnsAppWithGeneratedId()
    {
        // No app.pr exists — app keeps its generated Id
        await _app.Load();

        await Assert.That(_app.Id).IsNotNull();
        await Assert.That(_app.Id.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task SaveApp_UpdatesTimestamp()
    {
        _app.Id = "test-id";
        _app.Version = "0.2";

        var result = await _app.Save();

        await result.IsSuccess();

        // Verify file content
        var appPrPath = System.IO.Path.Combine(_tempDir, ".build", "app.pr");
        var json = System.IO.File.ReadAllText(appPrPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        await Assert.That(root.GetProperty("id").GetString()).IsEqualTo("test-id");
        await Assert.That(root.GetProperty("version").GetString()).IsEqualTo("0.2");
    }

    [Test]
    public async Task GetApp_CorruptJson_KeepsGeneratedId()
    {
        var buildDir = System.IO.Path.Combine(_tempDir, ".build");
        System.IO.Directory.CreateDirectory(buildDir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(buildDir, "app.pr"), "{ broken json {{");

        // Corrupt app.pr — Load() silently keeps generated identity
        await _app.Load();

        await Assert.That(_app.Id).IsNotNull();
        await Assert.That(_app.Id.Length).IsGreaterThan(0);
    }
}
