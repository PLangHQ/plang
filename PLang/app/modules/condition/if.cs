using app;
using app.variables;
using app.modules.condition.code;
using Action = app.goals.goal.steps.step.actions.action.@this;

namespace app.modules.condition;

[Action("if")]
public partial class If : IContext, IStep
{
    public partial data.@this? Left { get; init; }
    public partial data.@this<Operator> Operator { get; init; }
    public partial data.@this? Right { get; init; }
    [Default(false)]
    public partial data.@this<bool> Negate { get; init; }

    [Code]
    public partial IEvaluator Evaluator { get; }

    public async Task<data.@this> Run()
    {
        var evalResult = await Evaluator.Evaluate(this);
        // Evaluation errored → leave branchIndex unpublished (architect §5.7 / Batch 7 #6).
        if (!evalResult.Success) return evalResult;

        var conditionResult = evalResult.Value is true;
        if (Negate.Value) conditionResult = !conditionResult;

        // Mark indented sub-steps: disabled when false, clean when true
        var userStep = Step;
        if (userStep?.Goal != null)
        {
            var disableContext = Context.App!.System.Context;
            userStep.Goal.Steps.DisableChildrenOf(userStep, !conditionResult, disableContext);
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

        // Simple non-orchestrating form: 0/true for the truthy path, 1/false for the
        // skipped path. Paired label stays readable in coverage output ({true,false}).
        // Publish the full declared chain too so the report can show the untested half.
        var simple = Data(conditionResult);
        simple.Properties.Set("branchIndex", conditionResult ? 0 : 1);
        simple.Properties.Set("branchLabel", conditionResult ? "true" : "false");
        simple.Properties.Set("branchChain", new List<string> { "true", "false" });
        return simple;
    }

    /// <summary>
    /// Groups the step's actions into branches: [condition, actions...], [condition, actions...], [else actions...]
    /// Then evaluates conditions in order and runs the first matching branch.
    /// </summary>
    private async Task<data.@this> Orchestrate(
        app.goals.goal.steps.step.actions.@this actions, bool firstConditionResult)
    {
        int myIndex = actions.IndexOf(__action);
        if (myIndex < 0) myIndex = 0;

        var branches = actions.SplitAtConditions(myIndex);

        // Execute: first branch uses our already-evaluated result
        for (int b = 0; b < branches.Count; b++)
        {
            var (condition, body) = branches[b];
            bool branchResult;

            if (b == 0)
            {
                // First branch — head condition.if, already evaluated
                branchResult = firstConditionResult;
            }
            else if (condition != null
                && string.Equals(condition.ActionName, "else", StringComparison.OrdinalIgnoreCase))
            {
                // condition.else — no condition, always runs if we get here
                branchResult = true;
            }
            else if (condition != null)
            {
                // condition.elseif — dispatch the handler so its Evaluate + lifecycle fire
                var elseIfResult = await condition.RunAsync(Context);
                if (!elseIfResult.Success) return elseIfResult;
                branchResult = elseIfResult.Value is true;
            }
            else
            {
                // No explicit condition (trailing body-only tail) — treat as taken
                branchResult = true;
            }

            if (branchResult)
            {
                data.@this lastResult = global::app.data.@this<bool>.Ok(true);
                foreach (var action in body)
                {
                    lastResult = await action.RunAsync(Context);
                    if (!lastResult.Success) return lastResult;
                }
                // Record which branch fired. branchIndex is the position in the chain;
                // branchLabel mirrors the action name; branchChain is the full declared
                // shape so the report can show which other branches were never tested.
                var label = b == 0
                    ? "if"
                    : (condition != null && string.Equals(condition.ActionName, "else", StringComparison.OrdinalIgnoreCase))
                        ? "else"
                        : $"elseif[{b}]";
                lastResult.Properties.Set("branchIndex", b);
                lastResult.Properties.Set("branchLabel", label);
                lastResult.Properties.Set("branchChain", actions.ComputeBranchChain(myIndex));
                return lastResult;
            }
        }

        // No branch matched — only possible when there is no condition.else tail.
        // Leave branchIndex unset so coverage doesn't claim a branch fired.
        return Data(false);
    }
}
