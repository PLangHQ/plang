using app.variables;

using app.modules.code;

namespace app.modules.signing.code;

/// <summary>
/// Provider that owns the full signing/verification pipeline.
/// Default: Ed25519. Swappable for cloud KMS, hardware tokens, etc.
/// </summary>
public interface ISigning : IKey
{
    /// <summary>Full signing pipeline: get identity, hash, build envelope, sign.</summary>
    Task<data.@this> SignAsync(sign action);

    /// <summary>Full verification pipeline: type check, timeout, nonce, contracts, hash, verify.</summary>
    Task<data.@this> VerifyAsync(verify action);

    /// <summary>Low-level: signs bytes with the given private key. Returns signature bytes.</summary>
    data.@this Sign(byte[] data, string privateKey);

    /// <summary>Low-level: verifies a signature against data and public key.</summary>
    data.@this Verify(byte[] data, byte[] signature, string publicKey);
}
