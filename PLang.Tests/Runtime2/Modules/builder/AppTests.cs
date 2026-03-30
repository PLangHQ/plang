using System.Text.Json;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Utility;
using PLang.Runtime2.modules.builder;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.builder;

/// <summary>
/// Tests for builder.getApp and builder.saveApp — load/create and save app.pr metadata.
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
        var existingApp = new AppData
        {
            Id = "test-id-123",
            Created = DateTime.UtcNow.AddDays(-1),
            Updated = DateTime.UtcNow.AddDays(-1),
            Version = "0.2",
            Name = "TestApp"
        };
        var json = JsonSerializer.Serialize(existingApp, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        System.IO.File.WriteAllText(System.IO.Path.Combine(buildDir, "app.pr"), json);

        var action = new app { Context = _engine.Context, Path = "." };
        var result = await _engine.RunAction(action, _engine.Context);

        await Assert.That(result.Success).IsTrue();
        var app = result.Value as AppData;
        await Assert.That(app).IsNotNull();
        await Assert.That(app!.Id).IsEqualTo("test-id-123");
        await Assert.That(app.Name).IsEqualTo("TestApp");
    }

    [Test]
    public async Task GetApp_CreatesNewWhenMissing()
    {
        var action = new app { Context = _engine.Context, Path = "." };
        var result = await _engine.RunAction(action, _engine.Context);

        await Assert.That(result.Success).IsTrue();
        var app = result.Value as AppData;
        await Assert.That(app).IsNotNull();
        await Assert.That(app!.Id).IsNotNull();
        await Assert.That(app.Id.Length).IsGreaterThan(0);
        await Assert.That(app.Version).IsEqualTo("0.2");

        // File should exist on disk
        var appPrPath = System.IO.Path.Combine(_tempDir, ".build", "app.pr");
        await Assert.That(System.IO.File.Exists(appPrPath)).IsTrue();
    }

    [Test]
    public async Task SaveApp_UpdatesTimestamp()
    {
        var app = new AppData
        {
            Id = "test-id",
            Created = DateTime.UtcNow.AddDays(-1),
            Updated = DateTime.UtcNow.AddDays(-1),
            Version = "0.2"
        };
        var oldUpdated = app.Updated;

        var action = new appSave { Context = _engine.Context, App = app, Path = ".build/app.pr" };
        var result = await _engine.RunAction(action, _engine.Context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(app.Updated).IsGreaterThan(oldUpdated);

        // File should exist
        var appPrPath = System.IO.Path.Combine(_tempDir, ".build", "app.pr");
        await Assert.That(System.IO.File.Exists(appPrPath)).IsTrue();
    }
}
