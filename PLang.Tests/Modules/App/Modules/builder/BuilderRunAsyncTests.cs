using System.Text.Json;
using app.Utils;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Regression guard for the inverted-File.Exists fix.
///
/// <c>Builder.RunAsync</c> bootstraps the build. The branch under guard:
/// <code>if (!File.Exists(appPrPath) &amp;&amp; !_app.Create) { … }</code>
/// A previous version had the <c>!</c> inverted and fired the branch when
/// <c>app.pr</c> DID exist, forcing every build of an existing app to need
/// <c>--app={"create":true}</c>. Flipping the <c>!</c> back would silently
/// bring the original bug back; nothing else in the suite covers
/// <c>Builder.RunAsync</c>'s bootstrap.
///
/// dotnet (and TUnit) redirect stdin, so <c>Console.IsInputRedirected</c> is
/// true in this test process — that lands us on the headless-NoAppFound
/// branch deterministically without needing to mock channels.
/// </summary>
public class BuilderRunAsyncTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_runasync_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = TestApp.Create(_tempDir);
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

    private void WriteAppMarker()
    {
        var buildDir = System.IO.Path.Combine(_tempDir, ".build");
        System.IO.Directory.CreateDirectory(buildDir);
        var dummy = new { Id = Guid.NewGuid().ToString(), Created = DateTime.UtcNow };
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(buildDir, "app.pr"),
            JsonSerializer.Serialize(dummy, Json.CamelCaseIndented));
    }

    /// <summary>
    /// Bug shape: missing <c>app.pr</c>, no <c>--create</c>, headless stdin →
    /// must surface NoAppFound, not silently fall through. If the <c>!</c>
    /// flips back, this branch becomes <c>File.Exists(...) &amp;&amp; !_app.Create</c>
    /// — the condition becomes false (no marker present) and RunAsync would
    /// proceed to dispatch instead of erroring honestly here.
    /// </summary>
    [Test]
    public async Task RunAsync_MissingAppPr_HeadlessStdin_ReturnsNoAppFound()
    {
        await Assert.That(Console.IsInputRedirected)
            .IsTrue()
            .Because("test harness must redirect stdin for the headless branch to be reachable");

        var result = await _app.Builder.RunAsync();

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("NoAppFound");
    }

    /// <summary>
    /// Bug-revert dual: marker present + <c>Create=false</c> must not return
    /// NoAppFound from the bootstrap guard. If the <c>!</c> flips back, the
    /// guard incorrectly fires on an existing app and we get NoAppFound here.
    /// We don't assert RunAsync succeeds — the downstream dispatch to
    /// <c>system/builder/.build/build.pr</c> isn't present in this temp dir
    /// so it fails for unrelated reasons. The point is: not via NoAppFound.
    /// </summary>
    [Test]
    public async Task RunAsync_ExistingAppPr_DoesNotReturnNoAppFound()
    {
        WriteAppMarker();

        var result = await _app.Builder.RunAsync();

        // The build may fail (no system/builder tree in temp dir), but the
        // failure key must not be the bootstrap guard.
        await Assert.That(result.Error?.Key).IsNotEqualTo("NoAppFound");
    }
}
