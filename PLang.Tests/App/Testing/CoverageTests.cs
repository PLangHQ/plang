using global::App.Test;

namespace PLang.Tests.App.Testing;

/// <summary>
/// Batch 2 — Coverage tracker.
/// Each test's App has its own Coverage populated via AfterAction subscription.
/// At test end, the parent's Coverage absorbs the child's via Merge.
/// Two dimensions: module.action coverage (which handlers fired) and branch coverage
/// (which branch indices were observed at each condition.if site).
/// </summary>
public class CoverageTests
{
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::App.@this("/test");
    }

    // Fresh Coverage has no observed (module, action) entries.
    [Test]
    public async Task NewInstance_ModuleActions_ObservedIsEmpty()
    {
        var coverage = _app.Testing.Coverage;
        await Assert.That(coverage.ModuleActions.Any()).IsFalse();
    }

    // Fresh Coverage has no observed branch-site entries.
    [Test]
    public async Task NewInstance_Branches_ObservedIsEmpty()
    {
        var coverage = _app.Testing.Coverage;
        await Assert.That(coverage.Branches.Count).IsEqualTo(0);
    }

    // Recording a (module, action) pair adds it to the observed set; recording the
    // same pair twice is idempotent (set semantics, not a counter).
    [Test]
    public async Task RecordModuleAction_AddsObservationToSet()
    {
        var coverage = _app.Testing.Coverage;
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
        var coverage = _app.Testing.Coverage;
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
        var parent = _app.Testing.Coverage;
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
}
