using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.condition.providers;

namespace PLang.Runtime2.modules.condition;

[Action("compare")]
public partial class Compare : IContext
{
    public partial object? Left { get; init; }
    public partial string Operator { get; init; }
    public partial object? Right { get; init; }

    public Task<Data> Run()
    {
        var evaluator = new DefaultEvaluator();
        bool result = evaluator.Evaluate(Left, Operator, Right);
        return Task.FromResult(Data.Ok(result));
    }
}
