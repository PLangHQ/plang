using App.Engine.Variables;

using App.Engine.Providers;

namespace App.modules.signing.providers;

/// <summary>
/// Provider that owns the full signing/verification pipeline.
/// Default: Ed25519Provider. Swappable for cloud KMS, hardware tokens, etc.
/// </summary>
public interface ISigningProvider : IKeyProvider
{
    /// <summary>Full signing pipeline: get identity, hash, build envelope, sign.</summary>
    Task<Data> SignAsync(sign action);

    /// <summary>Full verification pipeline: type check, timeout, nonce, contracts, hash, verify.</summary>
    Task<Data> VerifyAsync(verify action);

    /// <summary>Low-level: signs bytes with the given private key. Returns signature bytes.</summary>
    Data Sign(byte[] data, string privateKey);

    /// <summary>Low-level: verifies a signature against data and public key.</summary>
    Data Verify(byte[] data, byte[] signature, string publicKey);
}
