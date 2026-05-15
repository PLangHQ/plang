using app;
using app.Variables;
using app.modules.condition.code;

namespace app.modules.condition;

[System.ComponentModel.Description("Additional condition branch evaluated when the preceding if condition is false")]
[Example("else if %a% > 5, write 'mid'",
    "condition.elseif Left([object] %a%), Operator([operator] >), Right([int] 5)")]
[Action("elseif")]
public partial class Elseif : IContext, IStep
{
    public partial Data.@this? Left { get; init; }
    public partial Data.@this<Operator> Operator { get; init; }
    public partial Data.@this? Right { get; init; }
    [Default(false)]
    public partial Data.@this<bool> Negate { get; init; }

    [Code]
    public partial IEvaluator Evaluator { get; }

    public Task<Data.@this> Run()
    {
        var evalResult = Evaluator.Evaluate(this);
        if (!evalResult.Success) return Task.FromResult(evalResult);
        var b = evalResult.Value is true;
        if (Negate.Value) b = !b;
        return Task.FromResult(Data(b));
    }
}
