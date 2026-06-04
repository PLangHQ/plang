using app.variable;
using app.module.code;

namespace app.module.crypto.code;

public interface ICrypto : ICode
{
    data.@this<global::app.module.crypto.type.hash.@this> Hash(Hash action);
    data.@this<bool> Verify(Verify action);
}
