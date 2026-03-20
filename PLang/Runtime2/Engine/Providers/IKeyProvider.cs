namespace PLang.Runtime2.Engine.Providers;

/// <summary>
/// Provider that can generate key pairs. Used by identity module for key generation delegation.
/// </summary>
public interface IKeyProvider : IProvider
{
    KeyPair GenerateKeyPair();
}
