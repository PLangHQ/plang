using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.condition.providers;

namespace PLang.Runtime2.modules.condition;

[Action("compare")]
public partial class Compare : IContext
{
    public partial Data? Left { get; init; }
    public partial string Operator { get; init; }
    public partial Data? Right { get; init; }

    [Provider]
    public partial IEvaluator Evaluator { get; }

    public Task<Data> Run() => Task.FromResult(Evaluator.Evaluate(this));
}
