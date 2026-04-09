using App.Variables;
using App.Providers;

namespace App.modules.assert.providers;

public interface IAssertProvider : IProvider
{
    Data.@this Equals(Equals action);
    Data.@this NotEquals(NotEquals action);
    Data.@this IsTrue(IsTrue action);
    Data.@this IsFalse(IsFalse action);
    Data.@this IsNull(IsNull action);
    Data.@this IsNotNull(IsNotNull action);
    Data.@this Contains(Contains action);
    Data.@this GreaterThan(GreaterThan action);
    Data.@this LessThan(LessThan action);
}
