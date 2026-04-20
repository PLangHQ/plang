using App;
using App.Variables;
using App.modules.condition.providers;
using Action = App.Goals.Goal.Steps.Step.Actions.Action.@this;

namespace App.modules.condition;

[Example("if %count% > 0\n    - call ProcessItems", "Left=%count%, Operator=>, Right=0")]
[Example("if %name% equals 'Alice'\n    - write 'Hello Alice'", "Left=%name%, Operator===, Right=Alice")]
[Example("if %x% > 5 set %b% = 4, else set %b% = 0", "Left=%x%, Operator=>, Right=5 (condition orchestrates then/else actions in same step)")]
[Action("if")]
public partial class If : IContext, IStep
{
    public partial Data.@this? Left { get; init; }
    public partial Data.@this<Operator> Operator { get; init; }
    public partial Data.@this? Right { get; init; }
    [Default(false)]
    public partial Data.@this<bool> Negate { get; init; }

    [Provider]
    public partial IEvaluator Evaluator { get; }

    public async Task<Data.@this> Run()
    {
        var evalResult = Evaluator.Evaluate(this);
        // Evaluation errored → leave branchIndex unpublished (architect §5.7 / Batch 7 #6).
        if (!evalResult.Success) return evalResult;

        var conditionResult = evalResult.Value is true;
        if (Negate.Value) conditionResult = !conditionResult;

        // Mark indented sub-steps: disabled when false, clean when true
        var userStep = Step;
        if (userStep?.Goal != null)
        {
            var disableContext = Context.App!.System.Context;
            var steps = userStep.Goal.Steps;
            for (int i = userStep.Index + 1; i < steps.Count; i++)
            {
                if (steps[i].Indent <= userStep.Indent) break;
                steps[i].Context = disableContext;
                steps[i].Disabled = !conditionResult;
            }
        }

        // Orchestrate if/elseif/else when there are multiple actions in this step.
        // Guard is step-scoped on Context._data (not Variables) so:
        //   1. PLang developers can't accidentally override it
        //   2. Inner goals called from branches get their own guard keys
        var actions = userStep?.Actions;
        var guardKey = $"__condition_orchestrating_{userStep?.GetHashCode()}__";
        var alreadyOrchestrating = Context.Get<bool>(guardKey);

        if (!alreadyOrchestrating && actions != null && actions.Count > 1)
        {
            Context.Set(guardKey, true);
            try
            {
                var result = await Orchestrate(actions, conditionResult);
                result.Handled = true;
                return result;
            }
            finally
            {
                Context[guardKey] = null;
            }
        }

        // Simple non-orchestrating form: 0 for true, 1 for false — uniform with multi-branch
        // indexing where 0 is the first (if) branch and later indices are elseif/else positions.
        var simple = Data(conditionResult);
        simple.Properties.Set("branchIndex", conditionResult ? 0 : 1);
        return simple;
    }

    /// <summary>
    /// Groups the step's actions into branches: [condition, actions...], [condition, actions...], [else actions...]
    /// Then evaluates conditions in order and runs the first matching branch.
    /// </summary>
    private async Task<Data.@this> Orchestrate(
        App.Goals.Goal.Steps.Step.Actions.@this actions, bool firstConditionResult)
    {
        // Find our position
        int myIndex = 0;
        for (int i = 0; i < actions.Count; i++)
        {
            if (ReferenceEquals(actions[i], __action))
            {
                myIndex = i;
                break;
            }
        }

        // Build branches: each branch is (conditionAction, thenActions[])
        // The last branch with no condition action is the else branch
        var branches = new List<(Action? condition, List<Action> body)>();
        List<Action>? currentBody = null;
        Action? currentCondition = null;

        for (int i = myIndex; i < actions.Count; i++)
        {
            if (IsConditionAction(actions[i]))
            {
                // Start a new branch
                if (currentBody != null)
                    branches.Add((currentCondition, currentBody));
                currentCondition = actions[i];
                currentBody = new List<Action>();
            }
            else
            {
                currentBody ??= new List<Action>();
                currentBody.Add(actions[i]);
            }
        }
        if (currentBody != null)
            branches.Add((currentCondition, currentBody));

        // Execute: first branch uses our already-evaluated result
        for (int b = 0; b < branches.Count; b++)
        {
            var (condition, body) = branches[b];
            bool branchResult;

            if (b == 0)
            {
                // First branch — we already evaluated this
                branchResult = firstConditionResult;
            }
            else if (condition != null)
            {
                // Elseif — evaluate the condition
                var elseIfResult = await condition.RunAsync(Context);
                if (!elseIfResult.Success) return elseIfResult;
                branchResult = elseIfResult.Value is true;
            }
            else
            {
                // Else — no condition, always runs if we get here
                branchResult = true;
            }

            if (branchResult)
            {
                Data.@this lastResult = Data(true);
                foreach (var action in body)
                {
                    lastResult = await action.RunAsync(Context);
                    if (!lastResult.Success) return lastResult;
                }
                // Record which branch fired (index within the if/elseif/else chain)
                // so the coverage subscriber can track branches per site.
                lastResult.Properties.Set("branchIndex", b);
                return lastResult;
            }
        }

        // No branch matched — rare (usually the else catches). Leave branchIndex unset
        // so coverage doesn't claim a branch fired.
        return Data(false);
    }

    private static bool IsConditionAction(Action action) =>
        string.Equals(action.Module, "condition", StringComparison.OrdinalIgnoreCase)
        && string.Equals(action.ActionName, "if", StringComparison.OrdinalIgnoreCase);
}
