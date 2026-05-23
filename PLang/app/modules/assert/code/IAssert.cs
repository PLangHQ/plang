using System.Threading.Tasks;
using app.variables;
using app.modules.code;

namespace app.modules.assert.code;

public interface IAssert : ICode
{
    // Every assert returns Data<bool>: Ok(true) on pass, FromError(AssertionError) on fail.
    data.@this<bool> Equals(Equals action);
    data.@this<bool> NotEquals(NotEquals action);
    // IsTrue/IsFalse are async — an asserted value may be IBooleanResolvable
    // (a path), whose truthiness is resolved with I/O. (codeanalyzer v1 F3)
    Task<data.@this<bool>> IsTrue(IsTrue action);
    Task<data.@this<bool>> IsFalse(IsFalse action);
    data.@this<bool> IsNull(IsNull action);
    data.@this<bool> IsNotNull(IsNotNull action);
    data.@this<bool> Contains(Contains action);
    data.@this<bool> NotContains(NotContains action);
    data.@this<bool> GreaterThan(GreaterThan action);
    data.@this<bool> LessThan(LessThan action);
}
