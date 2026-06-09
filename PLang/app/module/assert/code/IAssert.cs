using System.Threading.Tasks;
using app.variable;
using app.module.code;

namespace app.module.assert.code;

public interface IAssert : ICode
{
    // Every assert returns Data<bool>: Ok(true) on pass, FromError(AssertionError) on fail.
    // The comparing asserts are async — they route through data.Compare (the one
    // comparison entry); an asserted value may also be IBooleanResolvable (a path),
    // whose truthiness resolves with I/O.
    Task<data.@this<global::app.type.@bool.@this>> Equals(Equals action);
    Task<data.@this<global::app.type.@bool.@this>> NotEquals(NotEquals action);
    Task<data.@this<global::app.type.@bool.@this>> IsTrue(IsTrue action);
    Task<data.@this<global::app.type.@bool.@this>> IsFalse(IsFalse action);
    data.@this<global::app.type.@bool.@this> IsNull(IsNull action);
    data.@this<global::app.type.@bool.@this> IsNotNull(IsNotNull action);
    Task<data.@this<global::app.type.@bool.@this>> Contains(Contains action);
    Task<data.@this<global::app.type.@bool.@this>> NotContains(NotContains action);
    Task<data.@this<global::app.type.@bool.@this>> GreaterThan(GreaterThan action);
    Task<data.@this<global::app.type.@bool.@this>> LessThan(LessThan action);
}
