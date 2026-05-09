using App.Variables;

using App.Code;

namespace App.modules.signing.code;

/// <summary>
/// Provider that can generate key pairs. Used by identity module for key generation delegation.
/// Returns Data — never throws.
/// </summary>
public interface IKey : ICode
{
    Data.@this<KeyPair> GenerateKeyPair();
}
