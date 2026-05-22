using System.Threading.Tasks;
using app.variables;
using app.modules.code;

namespace app.modules.assert.code;

public interface IAssert : ICode
{
    data.@this Equals(Equals action);
    data.@this NotEquals(NotEquals action);
    // IsTrue/IsFalse are async — an asserted value may be IBooleanResolvable
    // (a path), whose truthiness is resolved with I/O. (codeanalyzer v1 F3)
    Task<data.@this> IsTrue(IsTrue action);
    Task<data.@this> IsFalse(IsFalse action);
    data.@this IsNull(IsNull action);
    data.@this IsNotNull(IsNotNull action);
    data.@this Contains(Contains action);
    data.@this NotContains(NotContains action);
    data.@this GreaterThan(GreaterThan action);
    data.@this LessThan(LessThan action);
}
