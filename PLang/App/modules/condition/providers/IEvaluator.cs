using App.Engine.Variables;
using App.Engine.Providers;

namespace App.modules.condition.providers;

public interface IEvaluator : IProvider
{
    Data Evaluate(If action);
    Data Evaluate(Compare action);
}
