using app.variables;
using app.modules.code;

namespace app.modules.condition.code;

public interface IEvaluator : ICode
{
    data.@this Evaluate(If action);
    data.@this Evaluate(Elseif action);
    data.@this Evaluate(Compare action);
}
