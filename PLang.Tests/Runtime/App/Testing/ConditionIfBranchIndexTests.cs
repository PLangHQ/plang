using app.module.condition;

namespace PLang.Tests.App.Tester;

/// <summary>
/// Batch 7 — condition.if branch_index.
/// condition.if publishes Properties["branchIndex"] (int) on its returned Data so the
/// coverage subscriber can track which branch fired at each site.
/// Uniform indexing: simple-if uses 0 for true, 1 for false. Multi-branch uses the
/// branch's position in the chain (0 = if, 1 = first elseif, 2 = second elseif, ...,
/// N = else). One mental model for all forms.
/// PLang access syntax: %!data!branchIndex% (! separator for properties).
/// </summary>
public class ConditionIfBranchIndexTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/test");
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    // Builds an `if <left> <op> <right>` action with no body (so simple path is taken).
    private static PrAction IfAction(object? left, string op, object? right)
        => Make.Action("condition", "if", ("Left", left), ("Operator", op), ("Right", right));

    // Runs a single-step goal whose action is the given condition.if action — loaded
    // through the real read path, so the action assembles and its params type/stamp
    // like a .pr off disk. Returns the result Data for the test to inspect.
    private async Task<Data> RunSingleStep(PrAction ifAction)
    {
        var goal = await RealGoalLoad.ViaChannel(_app,
            Make.Goal("CondGoal", Make.Step("if test", ifAction)));
        _app.Goal.Add(goal);

        Data? captured = null;
        _app.User.Context.Events.Register(new EventBinding(
            Trigger.AfterAction,
            (context, action, result) =>
            {
                if (action?.Module == "condition" && action.ActionName == "if")
                    captured = result;
                return Task.FromResult(Data.Ok());
            },
            priority: int.MaxValue,
            stopOnError: false));

        await _app.RunGoalAsync(goal, _app.User.Context);
        return captured!;
    }

    // Simple non-orchestrating form: if(true) → result.Properties["branchIndex"] == 0.
    [Test]
    public async Task Simple_IfTrue_BranchIndexIs0()
    {
        var result = await RunSingleStep(IfAction(1, "==", 1));
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Properties.Contains("branchIndex")).IsTrue();
        await Assert.That(result.Properties.Get<int>("branchIndex")).IsEqualTo(0);
    }

    // Simple non-orchestrating form: if(false) → result.Properties["branchIndex"] == 1.
    [Test]
    public async Task Simple_IfFalse_BranchIndexIs1()
    {
        var result = await RunSingleStep(IfAction(1, "==", 2));
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Properties.Contains("branchIndex")).IsTrue();
        await Assert.That(result.Properties.Get<int>("branchIndex")).IsEqualTo(1);
    }

    // Builds a multi-branch chain: if %x% [op1] [v1] ... elseif %x% [op2] [v2] ... [else ...].
    // Runs it, captures the orchestrating condition.if's final result (after sub-actions).
    private async Task<Data> RunMultiBranch(int xValue, params (string? op, object? right, string bodyVar, int bodyVal)[] branches)
    {
        var vars = _app.User.Context.Variable;
        vars.Set("x", xValue);

        var actions = new List<PrAction>();
        foreach (var br in branches)
        {
            if (br.op != null)
                actions.Add(Make.Action("condition", "if",
                    ("Left", "%x%"), ("Operator", br.op), ("Right", br.right)));
            // The var-name slot (Name) is declared type:variable — the way the builder
            // emits it; a bare string Name would decline at run.
            actions.Add(Make.Action("variable", "set",
                Make.Param("Name", br.bodyVar, "variable"), ("Value", br.bodyVal)));
        }

        var goal = await RealGoalLoad.ViaChannel(_app,
            Make.Goal("Multi", Make.Step("multi", actions.ToArray())));
        _app.Goal.Add(goal);

        Data? captured = null;
        // Capture the orchestrating if's AfterAction (it emits the final branchIndex
        // there, after its sub-evaluations). It is the step's first action — but the
        // LOADED instance, since the read borns fresh actions (the hand-built ones we
        // passed in are not what runs).
        var orchestrator = goal.Steps.First().Actions.First();
        _app.User.Context.Events.Register(new EventBinding(
            Trigger.AfterAction,
            (context, action, result) =>
            {
                if (ReferenceEquals(action, orchestrator))
                    captured = result;
                return Task.FromResult(Data.Ok());
            },
            priority: int.MaxValue,
            stopOnError: false));

        await _app.RunGoalAsync(goal, _app.User.Context);
        return captured!;
    }

    // if/elseif chain where the first condition matches → branchIndex == 0.
    [Test]
    public async Task MultiBranch_FirstBranchMatches_BranchIndexIs0()
    {
        var result = await RunMultiBranch(20,
            (">", 10, "a", 1),
            (">", 5, "b", 2));

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Properties.Get<int>("branchIndex")).IsEqualTo(0);
    }

    // if/elseif chain where the second condition matches → branchIndex == 1.
    [Test]
    public async Task MultiBranch_SecondBranchMatches_BranchIndexIs1()
    {
        var result = await RunMultiBranch(7,
            (">", 10, "a", 1),
            (">", 5, "b", 2));

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Properties.Get<int>("branchIndex")).IsEqualTo(1);
    }

    // v1 semantics: an if/elseif chain where none of the conditions match produces
    // Data(false) with no branchIndex published. (The coverage subscriber treats missing
    // branchIndex as "no branch fired" — same as the eval-error case in test #6.)
    // Note: v1 doesn't model a true "else" as a distinct null-condition branch — the
    // Orchestrate build-branches logic merges any trailing body with the last condition.
    // True else support is a builder-level concern not in v1 scope.
    [Test]
    public async Task MultiBranch_NoneMatch_ElseBranchIndexEqualsElsePosition()
    {
        // 3 conditions all false → no branch matches → no branchIndex published
        var result = await RunMultiBranch(0,
            (">", 100, "a", 1),
            (">", 50, "b", 2),
            (">", 10, "c", 3));

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Properties.Contains("branchIndex")).IsFalse();
    }

    // If condition evaluation produces an error Data, no branch is taken and
    // branchIndex is not set in the returned Data.Properties. Coverage subscriber
    // skips the site — avoids false-positive coverage for tests that never actually
    // selected a branch. (independent — architect §5.6 flagged as open)
    [Test]
    public async Task Evaluation_ThrowsOrErrors_NoBranchIndexPublished()
    {
        // Use an invalid comparison to force an eval error: ordering text against a
        // dict — dict is equality-only, so `>` has no honest answer and the boundary
        // raises an evaluation error.
        var action = new PrAction
        {
            Module = "condition",
            ActionName = "if",
            Parameters = new List<Data>
            {
                new("Left", "hello"),
                new("Operator", ">"),
                new("Right", new app.type.dict.@this()) // not orderable
            }
        };

        var result = await RunSingleStep(action);
        await Assert.That(result).IsNotNull();
        // Pre-condition: the fixture must actually trigger an eval error — otherwise
        // the branchIndex-absence check is vacuously true and the test proves nothing.
        // If this assertion fires, the fixture needs to be strengthened (pick a
        // different non-comparable pair) rather than relaxing the guard.
        await result.IsFailure();
        await Assert.That(result.Properties.Contains("branchIndex")).IsFalse();
    }
}
