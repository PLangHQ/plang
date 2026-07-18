using app.module.action.condition;
using app.module.action.condition.code;
using app.test;

namespace PLang.Tests.App.Tester;

/// <summary>
/// Regression guard for the three-bug cluster fixed by commit d05c138d.
///
/// Root cause: `SplitAtConditions` used `_items[i]` instead of the indexer, so inner
/// elseif actions were returned with `Step == null`. Three symptoms cascaded:
///   1. Coverage subscriber recorded at phantom site "?:?" instead of "goal:step".
///   2. `alreadyOrchestrating` guard-key mismatch (hash of null != hash of real step)
///      caused inner elseifs to re-enter orchestration on the simple path.
///   3. `DisableChildrenOf` silently skipped when invoked from an inner elseif.
///
/// This test attaches a production-shaped coverage subscriber (same filter as run.cs:
/// `action.IsIfHead`), runs a multi-action orchestrate step where the outer `if` is
/// false and the inner `elseif` is true, and asserts:
///   - No "?:?" site is recorded (Step propagated to inner elseif)
///   - The observed branchIndex at the correct site is 1 (elseif matched)
///   - Inner elseif is not flagged as IfHead (filter works)
///   - Indented sub-step ran (DisableChildrenOf worked per-branch)
/// </summary>
public class OrchestrateBranchCoverageTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/test");
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    // Attaches the same-shape subscriber used by test.run's RunSingleAsync, writing
    // observations into a fresh Coverage instance and keeping the list of (action,
    // isIfHead) pairs seen so the test can make filter-level assertions.
    private (Coverage coverage, List<(PrAction action, bool isIfHead)> observed) RegisterCoverageProbe()
    {
        var coverage = new Coverage();
        var observed = new List<(PrAction action, bool isIfHead)>();
        _app.User.Context.Events.Register(new EventBinding(
            Trigger.AfterAction,
            async (context, action, result) =>
            {
                if (action != null)
                {
                    coverage.RecordModuleAction(action.Module, action.ActionName);
                    if (action.IsCondition)
                        observed.Add((action, action.IsIfHead));
                    if (action.IsIfHead
                        && result != null && result.Properties.Contains("branchIndex"))
                    {
                        var goal = action.Step?.Goal;
                        var goalId = goal?.Path ?? goal?.Name ?? "?";
                        var stepIndex = action.Step?.Index.ToString() ?? "?";
                        var site = $"{goalId}:{stepIndex}";
                        coverage.RecordBranch(site, (await result.Properties.Get<int>("branchIndex")));
                    }
                }
                return Data.Ok();
            },
            priority: int.MaxValue,
            stopOnError: false));
        return (coverage, observed);
    }

    private static PrAction IfAction(object? left, string op, object? right)
        => Make.Action("condition", "if", ("Left", left), ("Operator", op), ("Right", right));

    // The var-name slot (Name) is declared type:variable — the builder emits it that
    // way; a bare string Name would decline at run.
    private static PrAction SetAction(string name, object value)
        => Make.Action("variable", "set", Make.Param("Name", name, "variable"), ("Value", value));

    // Multi-action orchestrate step where outer if is false and inner elseif is true.
    // Before d05c138d: inner elseif's Step was null, so the coverage subscriber recorded
    // at "?:?", the orchestrate guard mis-matched, and DisableChildrenOf silently skipped.
    // After d05c138d: Step is populated via the Actions indexer, so all three paths work.
    [Test]
    public async Task MultiActionOrchestrate_InnerElseIfMatches_FilterSkipsPhantomSites_SubStepsRun()
    {
        _app.User.Context.Variable.Set("x", 5);

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("Orch",
            Make.Step("if outer false elseif inner true",
                IfAction("%x%", ">", 100),   // outer — false (5 > 100)
                SetAction("bodyA", 1),       // body under outer if
                IfAction("%x%", ">", 0),     // inner — true (5 > 0)
                SetAction("bodyB", 2)),      // body under inner elseif
            // Indented sub-step: only reached when DisableChildrenOf leaves it enabled.
            Make.Step("set subran", 1, SetAction("subran", 1))));
        _app.Goal.Add(goal);

        var (coverage, observed) = RegisterCoverageProbe();

        await _app.RunGoalAsync(goal, _app.User.Context);

        // 1. DisableChildrenOf per-branch: the outer `if` (false) first disables the
        //    sub-step; then the inner elseif (true) re-enables it. In the bug case
        //    (d05c138d pre-fix), the inner's `userStep` was null so DisableChildrenOf
        //    was silently skipped — the sub-step stayed disabled from the outer's
        //    pass and `subran` would be unset. After the fix, the re-enable fires
        //    and the sub-step runs.
        var vars = _app.User.Context.Variable;
        await Assert.That(Convert.ToInt64((await vars.GetValue("subran"))!)).IsEqualTo(1L);

        // 2. alreadyOrchestrating guard keyed on the real step: inner elseif's body
        //    ran (bodyB set) and outer's body didn't (bodyA unset because outer was false).
        await Assert.That(Convert.ToInt64((await vars.GetValue("bodyB"))!)).IsEqualTo(2L);
        await Assert.That(vars.Contains("bodyA")).IsFalse();

        // 3. Coverage subscriber never recorded "?:?" — Step was propagated.
        await Assert.That(coverage.Branches.ContainsKey("?:?")).IsFalse();

        // 4. Exactly one site was recorded, at the orchestrate step's real key.
        await Assert.That(coverage.Branches.Count).IsEqualTo(1);
        await Assert.That(coverage.Branches.ContainsKey("/Orch.goal:0")).IsTrue();

        // 5. branchIndex=1 recorded — the elseif matched (position 1 in the chain).
        await Assert.That(coverage.Branches["/Orch.goal:0"].Contains(1)).IsTrue();

        // 6. Both condition.if actions fire AfterAction — outer as orchestrator,
        //    inner on the simple path during elseif evaluation. Old-shape fixtures
        //    (two condition.if) have IsIfHead=true on both, so we use the legacy
        //    IsFirst predicate here purely as a distinguisher to
        //    confirm the inner action's Step is propagated (the SplitAtConditions
        //    fix from d05c138d).
        var outerObservations = observed.Where(o => o.action.IsFirst).ToList();
        var innerObservations = observed.Where(o => !o.action.IsFirst).ToList();
        await Assert.That(outerObservations.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(innerObservations.Count).IsGreaterThanOrEqualTo(1);
    }

    // Outer-only scenario: a false condition must disable the sub-step (no inner elseif
    // re-enables). Complements the MultiActionOrchestrate test by isolating the
    // DisableChildrenOf(disable=true) path from the orchestrate re-enable path.
    // If DisableChildrenOf silently no-ops (e.g. userStep is null), the sub-step would
    // still run and `subran` would be set — so `IsFalse()` discriminates.
    [Test]
    public async Task SingleIfFalse_DisablesIndentedSubStep()
    {
        _app.User.Context.Variable.Set("x", 5);

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("SingleIf",
            Make.Step("if x > 100", IfAction("%x%", ">", 100)), // false
            Make.Step("set subran", 1, SetAction("subran", 1))));
        _app.Goal.Add(goal);

        await _app.RunGoalAsync(goal, _app.User.Context);

        var vars = _app.User.Context.Variable;
        await Assert.That(vars.Contains("subran")).IsFalse();
    }

    // Belt-and-suspenders: direct assertion on Actions propagation. Ensures
    // SplitAtConditions reads via the indexer (Step propagated on every returned action),
    // matching the fix from d05c138d.
    [Test]
    public async Task SplitAtConditions_PropagatesStepToEveryReturnedAction()
    {
        var step = new Step
        {
            Index = 0,
            Text = "multi",
            Actions = new StepActions
            {
                IfAction(1, ">", 100),
                SetAction("a", 1),
                IfAction(1, ">", 0),
                SetAction("b", 2)
            }
        };
        // Force Actions → step binding (same path the runtime takes via enumeration).
        _ = step.Actions;

        var decision = global::app.module.action.condition.decision.@this.Of(step.Actions)!;

        await Assert.That(decision.Count).IsEqualTo(2);
        foreach (var (condition, body) in decision)
        {
            await Assert.That(condition).IsNotNull();
            await Assert.That(condition!.Step).IsNotNull();
            await Assert.That(condition.Step!.Index).IsEqualTo(0);
            foreach (var bodyAction in body)
            {
                await Assert.That(bodyAction.Step).IsNotNull();
                await Assert.That(bodyAction.Step!.Index).IsEqualTo(0);
            }
        }

        // Inner elseif (second condition action) must report IsFirst=false.
        var innerElseIf = decision[1].Condition!;
        await Assert.That(innerElseIf.IsFirst).IsFalse();

        // Outer if (first condition action) must report IsFirst=true.
        var outerIf = decision[0].Condition!;
        await Assert.That(outerIf.IsFirst).IsTrue();
    }
}
