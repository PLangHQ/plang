using app.Variables;
using app.modules.condition.code;

namespace app.modules.condition;

[ModuleDescription("Conditional branching: evaluate comparisons and branch execution with if/elseif/else")]
[System.ComponentModel.Description("Compare two values with an operator and write the boolean result to a variable")]
[Example("compare %a% > %b%, write to %isGreater%",
    "condition.compare Left([object] %a%), Operator([operator] >), Right([object] %b%) | variable.set Name([string] %isGreater%), Value([object] %__data__%)")]
[Action("compare")]
public partial class Compare : IContext
{
    public partial Data.@this? Left { get; init; }
    public partial Data.@this<Operator> Operator { get; init; }
    public partial Data.@this? Right { get; init; }

    [Code]
    public partial IEvaluator Evaluator { get; }

    public Task<Data.@this> Run() => Task.FromResult(Evaluator.Evaluate(this));
}
