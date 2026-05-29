using System.Threading.Tasks;
using app.variable;
using app.modules.code;

namespace app.modules.condition.code;

// Evaluation is async: an operand may be IBooleanResolvable (a path), whose
// truthiness is resolved with I/O.
public interface IEvaluator : ICode
{
    Task<data.@this<bool>> Evaluate(If action);
    Task<data.@this<bool>> Evaluate(Elseif action);
    Task<data.@this<bool>> Evaluate(Compare action);
}
