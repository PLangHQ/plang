using App.Engine.Variables;
using App.modules.condition.providers;

namespace App.modules.condition;

[Example("compare %a% > %b%, write to %isGreater%", "Left=%a%, Operator=>, Right=%b%")]
[Example("compare %status% == 'active', write to %isActive%", "Left=%status%, Operator===, Right=active")]
[Action("compare")]
public partial class Compare : IContext
{
    public partial Data? Left { get; init; }
    public partial Operator Operator { get; init; }
    public partial Data? Right { get; init; }

    [Provider]
    public partial IEvaluator Evaluator { get; }

    public Task<Data> Run() => Task.FromResult(Evaluator.Evaluate(this));
}
