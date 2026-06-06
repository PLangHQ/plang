using System.Threading.Tasks;
using app.variable;
using app.module.code;

namespace app.module.assert.code;

public interface IAssert : ICode
{
    // Every assert returns Data<bool>: Ok(true) on pass, FromError(AssertionError) on fail.
    data.@this<global::app.type.@bool.@this> Equals(Equals action);
    data.@this<global::app.type.@bool.@this> NotEquals(NotEquals action);
    // IsTrue/IsFalse are async — an asserted value may be IBooleanResolvable
    // (a path), whose truthiness is resolved with I/O.
    Task<data.@this<global::app.type.@bool.@this>> IsTrue(IsTrue action);
    Task<data.@this<global::app.type.@bool.@this>> IsFalse(IsFalse action);
    data.@this<global::app.type.@bool.@this> IsNull(IsNull action);
    data.@this<global::app.type.@bool.@this> IsNotNull(IsNotNull action);
    data.@this<global::app.type.@bool.@this> Contains(Contains action);
    data.@this<global::app.type.@bool.@this> NotContains(NotContains action);
    data.@this<global::app.type.@bool.@this> GreaterThan(GreaterThan action);
    data.@this<global::app.type.@bool.@this> LessThan(LessThan action);
}
