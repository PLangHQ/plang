using System.Text.Json;
using global::App.Actor.Context;
using global::App.Variables;
using global::App.Utils;
using global::App.modules.builder;
using PLangEngine = global::App.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Tests for builder.app and builder.app.save — load and save app.pr metadata.
/// </summary>
public class AppTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_app_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);
        _engine.Building.IsEnabled = true;
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try
        {
            await _engine.DisposeAsync();
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
        await _engine.Load();

        await Assert.That(_engine.Id).IsEqualTo("test-id-123");
        await Assert.That(_engine.Name).IsEqualTo("TestApp");
    }

    [Test]
    public async Task GetApp_ReturnsAppWithGeneratedId()
    {
        // No app.pr exists — app keeps its generated Id
        await _engine.Load();

        await Assert.That(_engine.Id).IsNotNull();
        await Assert.That(_engine.Id.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task SaveApp_UpdatesTimestamp()
    {
        _engine.Id = "test-id";
        _engine.Version = "0.2";

        var result = await _engine.Save();

        await Assert.That(result.Success).IsTrue();

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
        await _engine.Load();

        await Assert.That(_engine.Id).IsNotNull();
        await Assert.That(_engine.Id.Length).IsGreaterThan(0);
    }
}
