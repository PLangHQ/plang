using System.Text.Json;
using global::app.Tester;

namespace PLang.Tests.App.Tester;

/// <summary>
/// Batch 13 — per-test metadata.
/// Every global::app.Tester.Run captures the builder version that produced its .pr, plus the
/// Goal.Hash from the .pr. Surfaced in results.json so tooling can correlate
/// drift: if the current plang builder is a newer version than the one that
/// produced a test's .pr, the report flags it. Drift is informational, not
/// enforced — users decide when to rebuild.
/// </summary>
public class TestMetadataTests
{
    private string _tempDir = null!;
    private global::app.@this _app = null!;
    private System.IO.MemoryStream _captureStream = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-meta-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        var fs = new global::app.FileSystem.Default.PLangFileSystem(_tempDir, "");
        _app = new global::app.@this(fs);
        _captureStream = new System.IO.MemoryStream();
        _app.User.Channels.Register(new StreamChannel(
            EngineChannels.Output, _captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });
    }

    [After(Test)]
    public async Task Teardown()
    {
        await _app.DisposeAsync();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    private string CapturedOutput() => System.Text.Encoding.UTF8.GetString(_captureStream.ToArray());

    private static global::app.Tester.Run NewRun(string name, string? builderVersion = null, string? goalHash = "deadbeef")
    {
        var run = new global::app.Tester.Run(new global::app.Tester.File
        {
            Path = $"Tests/{name}.test.goal",
            EntryGoalName = name,
            GoalHash = goalHash,
            BuilderVersion = builderVersion
        });
        run.Complete(global::app.Tester.Status.Pass);
        return run;
    }

    // The .pr file carries a builder-version field (written at build time). At
    // discovery, global::app.Tester.Run reads and stores it in its Metadata so the report can
    // surface "built by version X".
    [Test]
    public async Task Metadata_TestRun_CapturesBuilderVersionFromPr()
    {
        var run = NewRun("T", builderVersion: "v1.2.3");
        await Assert.That(run.File.BuilderVersion).IsEqualTo("v1.2.3");
    }

    // global::app.Tester.Run captures Goal.Hash (Name + Steps.Text SHA-256) from the .pr.
    // Combined with current-file Goal.Hash, enables drift correlation: "this
    // .pr was built from goal text at hash Y".
    [Test]
    public async Task Metadata_TestRun_CapturesGoalHashFromPr()
    {
        var run = NewRun("T", goalHash: "abc123");
        await Assert.That(run.File.GoalHash).IsEqualTo("abc123");
    }

    // results.json exposes the builder version per test run entry. Tooling can
    // spot tests built by an old builder version and prompt a rebuild. Surfacing
    // the field is a report contract — dropping it breaks downstream parsers.
    [Test]
    public async Task Metadata_Report_SurfacesBuilderVersion_InResultsJson()
    {
        _app.Tester.Results.Add(NewRun("T", builderVersion: "v1.0"));

        var action = new global::app.modules.test.report { Context = _app.User.Context };
        await action.Run();

        var jsonPath = System.IO.Path.Combine(_tempDir, ".test", "results.json");
        var json = await System.IO.File.ReadAllTextAsync(jsonPath);
        using var doc = JsonDocument.Parse(json);

        var runsArray = doc.RootElement.GetProperty("runs");
        var first = runsArray[0];
        await Assert.That(first.GetProperty("builderVersion").GetString()).IsEqualTo("v1.0");
    }

    // When the current plang builder version differs from the .pr's builder
    // version, the report flags the test with a "builder drift" note.
    // Non-fatal — drift correlation, not enforcement. Users see the flag and
    // can decide whether to rebuild on their own schedule.
    [Test]
    public async Task Metadata_Report_FlagsDriftWhenPrBuilderVersionMismatchesCurrent()
    {
        _app.Version = "v2.0"; // current app builder version
        _app.Tester.Results.Add(NewRun("T", builderVersion: "v1.0")); // stale

        var action = new global::app.modules.test.report { Context = _app.User.Context };
        await action.Run();

        var output = CapturedOutput();
        await Assert.That(output.Contains("builder drift")).IsTrue();
    }
}
