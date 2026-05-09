using App.Variables;
using App.Code;

namespace App.modules.assert.code;

public interface IAssert : ICode
{
    Data.@this Equals(Equals action);
    Data.@this NotEquals(NotEquals action);
    Data.@this IsTrue(IsTrue action);
    Data.@this IsFalse(IsFalse action);
    Data.@this IsNull(IsNull action);
    Data.@this IsNotNull(IsNotNull action);
    Data.@this Contains(Contains action);
    Data.@this NotContains(NotContains action);
    Data.@this GreaterThan(GreaterThan action);
    Data.@this LessThan(LessThan action);
}
