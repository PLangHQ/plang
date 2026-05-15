using app.Variables;

using app.Code;

namespace app.modules.signing.code;

/// <summary>
/// Provider that owns the full signing/verification pipeline.
/// Default: Ed25519. Swappable for cloud KMS, hardware tokens, etc.
/// </summary>
public interface ISigning : IKey
{
    /// <summary>Full signing pipeline: get identity, hash, build envelope, sign.</summary>
    Task<Data.@this> SignAsync(sign action);

    /// <summary>Full verification pipeline: type check, timeout, nonce, contracts, hash, verify.</summary>
    Task<Data.@this> VerifyAsync(verify action);

    /// <summary>Low-level: signs bytes with the given private key. Returns signature bytes.</summary>
    Data.@this Sign(byte[] data, string privateKey);

    /// <summary>Low-level: verifies a signature against data and public key.</summary>
    Data.@this Verify(byte[] data, byte[] signature, string publicKey);
}
