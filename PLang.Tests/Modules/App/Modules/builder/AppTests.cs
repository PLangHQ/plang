using System.Text.Json;
using app.actor.context;
using app.type.item.variable;
using app.Utils;
using app.module.build;
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
        _app = TestApp.Create(_tempDir);
        _app.Build = new global::app.module.build.@this(_app.actor.list.System.Context);
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

        // Golden: the reflected [Store] face is EXACTLY the 5 stamp fields (no App-graph leak,
        // no @schema envelope), indented (the goal-.pr writer path).
        await Assert.That(json).Contains("\n");
        await Assert.That(json).DoesNotContain("@schema");
        var keys = new System.Collections.Generic.HashSet<string>();
        foreach (var p in root.EnumerateObject()) keys.Add(p.Name);
        await Assert.That(keys).IsEquivalentTo(new[] { "id", "name", "created", "updated", "version" });
    }

    [Test]
    public async Task GetApp_CorruptJson_IsAnError_TheIdentityUntouched()
    {
        var buildDir = System.IO.Path.Combine(_tempDir, ".build");
        System.IO.Directory.CreateDirectory(buildDir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(buildDir, "app.pr"), "{ broken json {{");
        var idBefore = _app.Id;

        // A corrupt app.pr is an error naming it — never a quietly kept identity
        var loaded = await _app.Load();
        await Assert.That(loaded.Error?.Key).IsEqualTo("AppIdentityUnreadable");
        await Assert.That(_app.Id).IsEqualTo(idBefore);
    }

    [Test]
    public async Task GetApp_ReadsBackOnlyItsStoredFace()
    {
        // an app.pr naming more than the app writes sets nothing else: environment isn't part of the identity
        var buildDir = System.IO.Path.Combine(_tempDir, ".build");
        System.IO.Directory.CreateDirectory(buildDir);
        var before = _app.Environment;
        System.IO.File.WriteAllText(System.IO.Path.Combine(buildDir, "app.pr"),
            "{\"id\":\"id-7\",\"created\":\"2026-01-02T03:04:05Z\",\"environment\":\"hacked\",\"absolutePath\":\"/elsewhere\"}");

        var loaded = await _app.Load();

        await loaded.IsSuccess();
        await Assert.That(_app.Id).IsEqualTo("id-7");
        await Assert.That(_app.Created.Value).IsEqualTo(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        await Assert.That(_app.Environment).IsEqualTo(before);
        await Assert.That(_app.AbsolutePath).IsNotEqualTo("/elsewhere");
    }

    [Test]
    public async Task SaveThenLoad_ReadsBackWhatWasWritten()
    {
        _app.Id = "round-trip";
        _app.Version = "0.3";
        await (await _app.Save()).IsSuccess();
        var written = _app.Updated.Value;
        _app.Id = "changed";

        await (await _app.Load()).IsSuccess();

        await Assert.That(_app.Id).IsEqualTo("round-trip");
        await Assert.That(_app.Version).IsEqualTo("0.3");
        await Assert.That(_app.Updated.Value).IsEqualTo(written);
    }
}
