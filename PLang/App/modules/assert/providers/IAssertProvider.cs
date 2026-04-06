using App.Engine.Variables;
using App.Engine.Providers;

namespace App.modules.assert.providers;

public interface IAssertProvider : IProvider
{
    Data Equals(Equals action);
    Data NotEquals(NotEquals action);
    Data IsTrue(IsTrue action);
    Data IsFalse(IsFalse action);
    Data IsNull(IsNull action);
    Data IsNotNull(IsNotNull action);
    Data Contains(Contains action);
    Data GreaterThan(GreaterThan action);
    Data LessThan(LessThan action);
}
