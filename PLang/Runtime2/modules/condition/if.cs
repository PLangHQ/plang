using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.condition.providers;

namespace PLang.Runtime2.modules.condition;

[Example("if %count% > 0, call ProcessItems", "Left=%count%, Operator=>, Right=0, GoalIfTrue=ProcessItems")]
[Example("if %isAdmin%, call ShowAdminPanel, else call ShowUserPanel", "Left=%isAdmin%, GoalIfTrue=ShowAdminPanel, GoalIfFalse=ShowUserPanel")]
[Example("if %name% equals 'Alice'\n    - write 'Hello Alice'", "Left=%name%, Operator===, Right=Alice")]
[Action("if")]
public partial class If : IContext
{
    public partial Data? Left { get; init; }
    public partial Operator Operator { get; init; }
    public partial Data? Right { get; init; }
    public partial GoalCall? GoalIfTrue { get; init; }
    public partial GoalCall? GoalIfFalse { get; init; }
    [Default(false)]
    public partial bool Negate { get; init; }

    [Provider]
    public partial IEvaluator Evaluator { get; }

    public async Task<Data> Run()
    {
        var evalResult = Evaluator.Evaluate(this);
        if (!evalResult.Success) return evalResult;

        var conditionResult = evalResult.Value is true;
        if (Negate) conditionResult = !conditionResult;
        evalResult = Data.Ok(conditionResult);

        var goalToCall = conditionResult ? GoalIfTrue : GoalIfFalse;
        if (goalToCall != null)
        {
            var goalResult = await Context.Engine!.RunGoalAsync(goalToCall, Context, Context.CancellationToken);
            if (!goalResult.Success) return goalResult;
        }

        return evalResult;
    }
}
