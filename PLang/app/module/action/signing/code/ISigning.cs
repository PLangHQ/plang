using app.variable;

using app.module.action.code;

namespace app.module.action.signing.code;

/// <summary>
/// Provider that owns the full signing/verification pipeline.
/// Default: Ed25519. Swappable for cloud KMS, hardware tokens, etc.
/// </summary>
public interface ISigning : IKey
{
    /// <summary>Full signing pipeline: get identity, hash, build object, sign.</summary>
    Task<data.@this> SignAsync(sign action);

    /// <summary>Full verification pipeline: type check, timeout, nonce, contracts, hash, verify.</summary>
    Task<data.@this<global::app.type.item.@bool.@this>> VerifyAsync(verify action);

    /// <summary>Low-level crypto primitive: signs the unsigned signature's canonical bytes with the
    /// private key, returns the signature bytes. No context (shared provider) — throws on failure;
    /// the [Code] boundary wraps. Takes the signature whole; decomposes only at the NSec call.</summary>
    global::app.type.item.binary.@this Sign(global::app.type.item.signature.@this unsigned, global::app.type.item.text.@this privateKey);

    /// <summary>Low-level crypto primitive: true if the signature verifies against its own identity.
    /// Throws on bad key/signature input; the [Code] boundary maps false → SignatureInvalid. Takes
    /// the signature whole — Identity is the public key, ToSigningBytes the payload.</summary>
    global::app.type.item.@bool.@this Verify(global::app.type.item.signature.@this signature);
}
