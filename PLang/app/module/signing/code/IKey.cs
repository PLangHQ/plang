using app.variable;

using app.module.code;

namespace app.module.signing.code;

/// <summary>
/// Provider that can generate key pairs. Used by identity module for key generation delegation.
/// A KeyPair never crosses the PLang boundary (the user gets an Identity), so this is an
/// internal C# result — a (keys, error) tuple, not a Data. Never throws.
/// </summary>
public interface IKey : ICode
{
    (KeyPair? keys, global::app.error.IError? error) GenerateKeyPair();
}
