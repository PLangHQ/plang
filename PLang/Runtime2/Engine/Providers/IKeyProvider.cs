using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine.Providers;

/// <summary>
/// Provider that can generate key pairs. Used by identity module for key generation delegation.
/// Returns Data — never throws.
/// </summary>
public interface IKeyProvider : IProvider
{
    Data<KeyPair> GenerateKeyPair();
}
