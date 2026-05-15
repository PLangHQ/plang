using app.Variables;
using app.Code;

namespace app.modules.crypto.code;

public interface ICrypto : ICode
{
    Data.@this Hash(Hash action);
    Data.@this Verify(Verify action);
}
