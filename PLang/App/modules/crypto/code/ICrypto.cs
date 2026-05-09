using App.Variables;
using App.Code;

namespace App.modules.crypto.code;

public interface ICrypto : ICode
{
    Data.@this Hash(Hash action);
    Data.@this Verify(Verify action);
}
