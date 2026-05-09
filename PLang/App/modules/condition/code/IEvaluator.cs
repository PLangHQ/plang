using App.Variables;
using App.Code;

namespace App.modules.condition.code;

public interface IEvaluator : ICode
{
    Data.@this Evaluate(If action);
    Data.@this Evaluate(Elseif action);
    Data.@this Evaluate(Compare action);
}
