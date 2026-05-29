using app.variable;

using app.modules.code;

namespace app.modules.signing.code;

/// <summary>
/// Provider that can generate key pairs. Used by identity module for key generation delegation.
/// Returns Data — never throws.
/// </summary>
public interface IKey : ICode
{
    data.@this<KeyPair> GenerateKeyPair();
}
