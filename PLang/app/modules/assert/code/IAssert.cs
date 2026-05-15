using app.variables;
using app.Code;

namespace app.modules.assert.code;

public interface IAssert : ICode
{
    data.@this Equals(Equals action);
    data.@this NotEquals(NotEquals action);
    data.@this IsTrue(IsTrue action);
    data.@this IsFalse(IsFalse action);
    data.@this IsNull(IsNull action);
    data.@this IsNotNull(IsNotNull action);
    data.@this Contains(Contains action);
    data.@this NotContains(NotContains action);
    data.@this GreaterThan(GreaterThan action);
    data.@this LessThan(LessThan action);
}
