using App.Variables;
using App.modules.condition.providers;

namespace App.modules.condition;

[Example("compare %a% > %b%, write to %isGreater%", "Left=%a%, Operator=>, Right=%b%")]
[Example("compare %status% == 'active', write to %isActive%", "Left=%status%, Operator===, Right=active")]
[Action("compare")]
public partial class Compare : IContext
{
    public partial Data.@this? Left { get; init; }
    public partial Operator Operator { get; init; }
    public partial Data.@this? Right { get; init; }

    [Provider]
    public partial IEvaluator Evaluator { get; }

    public Task<Data.@this> Run() => Task.FromResult(Evaluator.Evaluate(this));
}
