using global::app.Tester;

namespace PLang.Tests.App.Tester;

/// <summary>
/// Batch 2 — Coverage tracker.
/// Each test's App has its own Coverage populated via AfterAction subscription.
/// At test end, the parent's Coverage absorbs the child's via Merge.
/// Two dimensions: module.action coverage (which handlers fired) and branch coverage
/// (which branch indices were observed at each condition.if site).
/// </summary>
public class CoverageTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/test");
    }

    // Fresh Coverage has no observed (module, action) entries.
    [Test]
    public async Task NewInstance_ModuleActions_ObservedIsEmpty()
    {
        var coverage = _app.Tester.Coverage;
        await Assert.That(coverage.ModuleActions.Any()).IsFalse();
    }

    // Fresh Coverage has no observed branch-site entries.
    [Test]
    public async Task NewInstance_Branches_ObservedIsEmpty()
    {
        var coverage = _app.Tester.Coverage;
        await Assert.That(coverage.Branches.Count).IsEqualTo(0);
    }

    // Recording a (module, action) pair adds it to the observed set; recording the
    // same pair twice is idempotent (set semantics, not a counter).
    [Test]
    public async Task RecordModuleAction_AddsObservationToSet()
    {
        var coverage = _app.Tester.Coverage;
        coverage.RecordModuleAction("http", "request");
        coverage.RecordModuleAction("http", "request");
        coverage.RecordModuleAction("variable", "set");

        var observed = coverage.ModuleActions.ToList();
        await Assert.That(observed.Count).IsEqualTo(2);
        await Assert.That(observed.Contains(("http", "request"))).IsTrue();
        await Assert.That(observed.Contains(("variable", "set"))).IsTrue();
    }

    // A single site (identified by goal:step location) observing branch_index=0
    // then later branch_index=1 accumulates the set {0, 1} for that site.
    [Test]
    public async Task RecordBranch_AtSameSite_AccumulatesIndicesObserved()
    {
        var coverage = _app.Tester.Coverage;
        coverage.RecordBranch("GoalA:3", 0);
        coverage.RecordBranch("GoalA:3", 1);
        coverage.RecordBranch("GoalA:3", 0); // idempotent

        var observed = coverage.Branches["GoalA:3"];
        await Assert.That(observed.Count).IsEqualTo(2);
        await Assert.That(observed.Contains(0)).IsTrue();
        await Assert.That(observed.Contains(1)).IsTrue();
    }

    // parent.Merge(child) unions both trackers — module.action pairs and branch-site
    // indices. This is the merge point between per-test isolation and the run-wide view.
    [Test]
    public async Task Merge_FromChildCoverage_UnionsModuleActionsAndBranches()
    {
        var parent = _app.Tester.Coverage;
        parent.RecordModuleAction("http", "request");
        parent.RecordBranch("G:1", 0);

        var child = new Coverage();
        child.RecordModuleAction("llm", "query");
        child.RecordBranch("G:1", 1); // same site, different index
        child.RecordBranch("H:5", 0); // new site

        parent.Merge(child);

        var actions = parent.ModuleActions.ToList();
        await Assert.That(actions.Contains(("http", "request"))).IsTrue();
        await Assert.That(actions.Contains(("llm", "query"))).IsTrue();

        var g1 = parent.Branches["G:1"];
        await Assert.That(g1.Contains(0)).IsTrue();
        await Assert.That(g1.Contains(1)).IsTrue();
        await Assert.That(parent.Branches["H:5"].Contains(0)).IsTrue();
    }

    // RecordBranchLabel exposes human-readable branch labels per site. Same site
    // accumulates labels (e.g. {"if", "elseif[1]"}) so a report can show which
    // specific branches fired and which were never tested.
    [Test]
    public async Task RecordBranchLabel_SiteAndLabel_Recorded()
    {
        var coverage = _app.Tester.Coverage;
        coverage.RecordBranchLabel("G:3", "if");

        await Assert.That(coverage.BranchLabels.ContainsKey("G:3")).IsTrue();
        await Assert.That(coverage.BranchLabels["G:3"].Contains("if")).IsTrue();
    }

    // Two different labels at the same site accumulate — both get rendered in reports.
    [Test]
    public async Task RecordBranchLabel_SameSite_Multiple_Accumulated()
    {
        var coverage = _app.Tester.Coverage;
        coverage.RecordBranchLabel("G:3", "if");
        coverage.RecordBranchLabel("G:3", "elseif[1]");
        coverage.RecordBranchLabel("G:3", "if"); // idempotent — set semantics

        var labels = coverage.BranchLabels["G:3"];
        await Assert.That(labels.Count).IsEqualTo(2);
        await Assert.That(labels.Contains("if")).IsTrue();
        await Assert.That(labels.Contains("elseif[1]")).IsTrue();
    }

    // RecordBranchChain records the declared chain at a site. First fire wins —
    // re-seeding with a different chain is a silent no-op (seed-then-observe safety).
    [Test]
    public async Task RecordBranchChain_FirstWins_SecondIgnored()
    {
        var coverage = _app.Tester.Coverage;
        coverage.RecordBranchChain("G:3", new List<string> { "if", "elseif[1]", "else" });
        coverage.RecordBranchChain("G:3", new List<string> { "true", "false" }); // ignored

        var chain = coverage.BranchChains["G:3"];
        await Assert.That(chain.Count).IsEqualTo(3);
        await Assert.That(chain[0]).IsEqualTo("if");
        await Assert.That(chain[1]).IsEqualTo("elseif[1]");
        await Assert.That(chain[2]).IsEqualTo("else");
    }

    // RecordBranchChain ignores null/empty input — guards the merge path when a
    // caller (condition.if on the simple-path eval-error branch) publishes an empty chain.
    [Test]
    public async Task RecordBranchChain_EmptyOrNull_Ignored()
    {
        var coverage = _app.Tester.Coverage;
        coverage.RecordBranchChain("G:3", new List<string>());
        coverage.RecordBranchChain("G:3", null!);

        await Assert.That(coverage.BranchChains.ContainsKey("G:3")).IsFalse();
    }

    // Merge unions branch labels across parent and child trackers. Both sides'
    // labels remain observable after the merge — nothing dropped on collision.
    [Test]
    public async Task Merge_UnionsBranchLabels()
    {
        var parent = _app.Tester.Coverage;
        parent.RecordBranchLabel("G:1", "if");

        var child = new Coverage();
        child.RecordBranchLabel("G:1", "elseif[1]"); // same site
        child.RecordBranchLabel("H:2", "true");       // new site

        parent.Merge(child);

        var g1 = parent.BranchLabels["G:1"];
        await Assert.That(g1.Count).IsEqualTo(2);
        await Assert.That(g1.Contains("if")).IsTrue();
        await Assert.That(g1.Contains("elseif[1]")).IsTrue();
        await Assert.That(parent.BranchLabels["H:2"].Contains("true")).IsTrue();
    }

    // Merge unions branch chains, first-seeder wins on collision. Symmetric with
    // RecordBranchChain's first-wins semantics.
    [Test]
    public async Task Merge_UnionsBranchChains_FirstWins()
    {
        var parent = _app.Tester.Coverage;
        parent.RecordBranchChain("G:1", new List<string> { "if", "else" });

        var child = new Coverage();
        // Child reseeds same site with a different chain — parent's wins (first).
        child.RecordBranchChain("G:1", new List<string> { "true", "false" });
        // Child adds a brand-new site — that gets added.
        child.RecordBranchChain("H:2", new List<string> { "if", "elseif[1]", "elseif[2]" });

        parent.Merge(child);

        var g1 = parent.BranchChains["G:1"];
        await Assert.That(g1.Count).IsEqualTo(2);
        await Assert.That(g1[0]).IsEqualTo("if");
        await Assert.That(g1[1]).IsEqualTo("else");

        var h2 = parent.BranchChains["H:2"];
        await Assert.That(h2.Count).IsEqualTo(3);
        await Assert.That(h2[0]).IsEqualTo("if");
    }
}
