using app.variables;
using app.modules.code;

namespace app.modules.crypto.code;

public interface ICrypto : ICode
{
    data.@this<byte[]> Hash(Hash action);
    data.@this<bool> Verify(Verify action);
}
