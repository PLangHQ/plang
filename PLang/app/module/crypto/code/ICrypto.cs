using app.variable;
using app.module.code;

namespace app.module.crypto.code;

public interface ICrypto : ICode
{
    Task<data.@this<global::app.module.crypto.type.hash.@this>> Hash(Hash action);
    Task<data.@this<global::app.type.@bool.@this>> Verify(Verify action);
}
