namespace PLang.Tests.App.Testing;

/// <summary>
/// Batch 13 — per-test metadata.
/// Every TestRun captures the builder version that produced its .pr, plus the
/// Goal.Hash from the .pr. Surfaced in results.json so tooling can correlate
/// drift: if the current plang builder is a newer version than the one that
/// produced a test's .pr, the report flags it. Drift is informational, not
/// enforced — users decide when to rebuild.
/// </summary>
public class TestMetadataTests
{
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::App.@this("/test");
    }

    // The .pr file carries a builder-version field (written at build time). At
    // discovery, TestRun reads and stores it in its Metadata so the report can
    // surface "built by version X".
    [Test]
    public async Task Metadata_TestRun_CapturesBuilderVersionFromPr()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // TestRun captures Goal.Hash (Name + Steps.Text SHA-256) from the .pr.
    // Combined with current-file Goal.Hash, enables drift correlation: "this
    // .pr was built from goal text at hash Y".
    [Test]
    public async Task Metadata_TestRun_CapturesGoalHashFromPr()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // results.json exposes the builder version per test run entry. Tooling can
    // spot tests built by an old builder version and prompt a rebuild. Surfacing
    // the field is a report contract — dropping it breaks downstream parsers.
    [Test]
    public async Task Metadata_Report_SurfacesBuilderVersion_InResultsJson()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // When the current plang builder version differs from the .pr's builder
    // version, the report flags the test with a "builder drift" note.
    // Non-fatal — drift correlation, not enforcement. Users see the flag and
    // can decide whether to rebuild on their own schedule.
    [Test]
    public async Task Metadata_Report_FlagsDriftWhenPrBuilderVersionMismatchesCurrent()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
