using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;

namespace PLang.Runtime2.modules.condition.providers;

public interface IEvaluator : IProvider
{
    Data Evaluate(If action);
    Data Evaluate(Compare action);
}
