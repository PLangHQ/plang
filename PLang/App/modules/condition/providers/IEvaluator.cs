using App.Variables;
using App.Providers;

namespace App.modules.condition.providers;

public interface IEvaluator : IProvider
{
    Data.@this Evaluate(If action);
    Data.@this Evaluate(Elseif action);
    Data.@this Evaluate(Compare action);
}
