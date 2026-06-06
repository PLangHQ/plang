using System.Threading.Tasks;
using app.variable;
using app.module.code;

namespace app.module.condition.code;

// Evaluation is async: an operand may be IBooleanResolvable (a path), whose
// truthiness is resolved with I/O.
public interface IEvaluator : ICode
{
    Task<data.@this<global::app.type.@bool.@this>> Evaluate(If action);
    Task<data.@this<global::app.type.@bool.@this>> Evaluate(Elseif action);
    Task<data.@this<global::app.type.@bool.@this>> Evaluate(Compare action);
}
