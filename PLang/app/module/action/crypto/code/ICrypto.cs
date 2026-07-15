using app.variable;
using app.module.action.code;

namespace app.module.action.crypto.code;

public interface ICrypto : ICode
{
    Task<data.@this<global::app.module.action.crypto.type.hash.@this>> Hash(Hash action);
    Task<data.@this<global::app.type.item.@bool.@this>> Verify(Verify action);
}
