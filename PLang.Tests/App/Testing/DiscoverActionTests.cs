namespace PLang.Tests.App.Testing;

/// <summary>
/// Batch 8 — test.discover action.
/// C# handler: filesystem walk + .pr parsing. Inputs: Path (default "."), Pattern
/// (default "*.test.goal"). Returns List&lt;TestFile&gt; with file path, entry goal,
/// .pr path, tags, status.
///
/// Freshness uses Goal.Hash (SHA-256 over Name + Steps.Text, [Store]-persisted in
/// .pr). Comment-only edits to a .goal DO NOT trigger stale — only Name/Step.Text
/// changes do. This is intentional: comments and whitespace shouldn't force rebuilds.
///
/// Auto-tags come from [RequiresCapability] on action handlers. Tags propagate across
/// sub-goals reached via static goal.call chains; dynamic goal names (via %var%) are
/// not traversed.
/// </summary>
public class DiscoverActionTests
{
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::App.@this("/test");
    }

    // Walks the tree of *.test.goal files under the target path; every match surfaces
    // in the returned List<TestFile>.
    [Test]
    public async Task Discover_RecursiveWalk_FindsAllTestGoalFiles()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // A .goal with no matching .pr in .build/ → TestStatus.Stale with reason "no .pr".
    [Test]
    public async Task Discover_NoPrFile_MarksStaleWithReasonNoPr()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // Fresh Goal.Hash (from current .goal) differs from the hash stored in the .pr
    // (Name or Step.Text changed since last build) → TestStatus.Stale with reason
    // "rebuild needed". Comment-only edits do NOT trigger stale.
    [Test]
    public async Task Discover_GoalAndPrHashMismatch_MarksStaleRebuildNeeded()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // Scans .pr for test.tag actions, collects their Tags parameter into the test's
    // user-tag set. Multiple test.tag actions accumulate.
    [Test]
    public async Task Discover_UserTags_ExtractedFromTestTagActionInPr()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // For each action in the .pr, resolves the handler class (via App.Modules.
    // GetCodeGenerated) and reads [RequiresCapability] via reflection. Capabilities
    // union into the test's auto-tag set. e.g. a test using http.request gains "network".
    [Test]
    public async Task Discover_AutoTags_ExtractedFromHandlerAttributes()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // Sub-goal reached via static goal.call: its actions' capabilities propagate up
    // to the caller test. Test uses goal.call "Helper" where Helper uses http.request
    // → test gains "network" auto-tag even though its own entry goal doesn't use http.
    [Test]
    public async Task Discover_AutoTags_TraverseSubGoals_UnionsAcrossCallChain()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // Config.Include=["fast"]: tests without the "fast" tag are returned as
    // TestStatus.Skipped — not removed from the list, so the run reports them as
    // skipped (CI visibility).
    [Test]
    public async Task Discover_IncludeFilter_NonMatchingTests_MarkedSkipped()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // Config.Exclude=["slow"]: tests carrying the "slow" tag are returned as
    // TestStatus.Skipped.
    [Test]
    public async Task Discover_ExcludeFilter_MatchingTests_MarkedSkipped()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // Filter composition: Include=["http"], Exclude=["slow"]. A test tagged
    // [http, slow] ends up Skipped — exclude wins on conflict.
    // (independent — boundary, locks the filter-order semantics)
    [Test]
    public async Task Discover_IncludeAndExclude_ExcludeAppliedAfterInclude()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // Robustness: a path that doesn't exist does not throw; returns empty list.
    // Logged for diagnostics but not fatal — discovery is non-destructive.
    // (independent — robustness)
    [Test]
    public async Task Discover_NonExistentPath_ReturnsEmptyList_NoError()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
