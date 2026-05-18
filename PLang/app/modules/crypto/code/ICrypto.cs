using app.variables;
using app.modules.code;

namespace app.modules.crypto.code;

public interface ICrypto : ICode
{
    data.@this Hash(Hash action);
    data.@this Verify(Verify action);
}
