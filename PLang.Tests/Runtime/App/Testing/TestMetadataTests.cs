using System.Text.Json;
using app.test;

namespace PLang.Tests.App.Tester;

/// <summary>
/// Batch 13 — per-test metadata.
/// Every global::app.test.Run captures the builder version that produced its .pr, plus the
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
        _app = TestApp.Create(_tempDir);
        _captureStream = new System.IO.MemoryStream();
        _app.User.Channel.Register(new StreamChannel(
            global::app.channel.list.@this.Output, _captureStream,
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

    private static global::app.test.@this NewTest(string name, string? builderVersion = null, string? goalHash = "deadbeef")
    {
        var goal = new Goal
        {
            Name = name,
            Path = global::app.type.item.path.@this.Resolve($"/Tests/{name}.test.goal", global::PLang.Tests.TestApp.SharedContext),
            Hash = goalHash,
            BuilderVersion = builderVersion
        };
        var test = new global::app.test.@this(global::PLang.Tests.TestApp.SharedContext) { Goal = goal };
        test.Complete(global::app.test.Status.Pass);
        return test;
    }

    // The .pr file carries a builder-version field (written at build time). At
    // discovery, global::app.test.Run reads and stores it in its Metadata so the report can
    // surface "built by version X".
    [Test]
    public async Task Metadata_TestRun_CapturesBuilderVersionFromPr()
    {
        var test = NewTest("T", builderVersion: "v1.2.3");
        await Assert.That(test.Goal.BuilderVersion).IsEqualTo("v1.2.3");
    }

    // global::app.test.Run captures Goal.Hash (Name + Steps.Text SHA-256) from the .pr.
    // Combined with current-file Goal.Hash, enables drift correlation: "this
    // .pr was built from goal text at hash Y".
    [Test]
    public async Task Metadata_TestRun_CapturesGoalHashFromPr()
    {
        var test = NewTest("T", goalHash: "abc123");
        await Assert.That(test.Goal.Hash).IsEqualTo("abc123");
    }

    // builderVersion is internal build bookkeeping ([Store, Debug] on Goal) — it is
    // deliberately NOT in results.json, which is the [Out] view we hand the user.
    // Drift is surfaced in the console (Metadata_Report_FlagsDrift...), reading
    // Goal.BuilderVersion directly — the artifact doesn't need to carry it.

    // When the current plang builder version differs from the .pr's builder
    // version, the report flags the test with a "builder drift" note.
    // Non-fatal — drift correlation, not enforcement. Users see the flag and
    // can decide whether to rebuild on their own schedule.
    [Test]
    public async Task Metadata_Report_FlagsDriftWhenPrBuilderVersionMismatchesCurrent()
    {
        _app.Version = "v2.0"; // current app builder version
        _app.Test.Add(NewTest("T", builderVersion: "v1.0")); // stale

        var action = new global::app.module.test.report(_app.User.Context);
        await action.Run();

        var output = CapturedOutput();
        await Assert.That(output.Contains("builder drift")).IsTrue();
    }
}
