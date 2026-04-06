using App;
using App.Variables;
using App.modules.condition.providers;

namespace App.modules.condition;

[Example("if %count% > 0, call ProcessItems", "Left=%count%, Operator=>, Right=0, GoalIfTrue=ProcessItems")]
[Example("if %isAdmin%, call ShowAdminPanel, else call ShowUserPanel", "Left=%isAdmin%, Operator===, Right=true, GoalIfTrue=ShowAdminPanel, GoalIfFalse=ShowUserPanel")]
[Example("if not %flag%, call HandleFalse", "Left=%flag%, Operator===, Right=true, Negate=true, GoalIfTrue=HandleFalse")]
[Example("if %name% equals 'Alice'\n    - write 'Hello Alice'", "Left=%name%, Operator===, Right=Alice")]
[Action("if")]
public partial class If : IContext, IStep
{
    public partial Data.@this? Left { get; init; }
    public partial Operator Operator { get; init; }
    public partial Data.@this? Right { get; init; }
    public partial GoalCall? GoalIfTrue { get; init; }
    public partial GoalCall? GoalIfFalse { get; init; }
    [Default(false)]
    public partial bool Negate { get; init; }

    [Provider]
    public partial IEvaluator Evaluator { get; }

    public async Task<Data.@this> Run()
    {
        var evalResult = Evaluator.Evaluate(this);
        if (!evalResult.Success) return evalResult;

        var conditionResult = evalResult.Value is true;
        if (Negate) conditionResult = !conditionResult;
        evalResult = Data.@this.Ok(conditionResult);

        // Mark indented sub-steps: disabled when false, clean when true
        // Step comes from IStep capability (action.Step = the user step being executed)
        var userStep = Step;
        if (userStep?.Goal != null)
        {
            // Use system context for disabled flags — the GoalSteps enumerator
            // runs on system context (run.pr iterates steps)
            var disableContext = Context.App!.System.Context;
            var steps = userStep.Goal.Steps;
            for (int i = userStep.Index + 1; i < steps.Count; i++)
            {
                if (steps[i].Indent <= userStep.Indent) break;
                steps[i].Context = disableContext;
                steps[i].Disabled = !conditionResult;
            }
        }

        var goalToCall = conditionResult ? GoalIfTrue : GoalIfFalse;
        if (goalToCall != null)
        {
            var goalResult = await Context.App!.RunGoalAsync(goalToCall, Context, Context.CancellationToken);
            if (!goalResult.Success) return goalResult;
        }

        return evalResult;
    }
}
