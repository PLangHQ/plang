using App.Variables;

using App.Providers;

namespace App.modules.signing.providers;

/// <summary>
/// Provider that can generate key pairs. Used by identity module for key generation delegation.
/// Returns Data — never throws.
/// </summary>
public interface IKeyProvider : IProvider
{
    Data<KeyPair> GenerateKeyPair();
}
