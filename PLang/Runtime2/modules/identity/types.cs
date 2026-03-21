using PLang.Runtime2.Engine;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Represents a PLang identity (key pair with metadata).
/// Pure data class — persistence is owned by IIdentityProvider.
/// </summary>
public sealed class IdentityVariable
{
    /// <summary>Display name for this identity (e.g., "default", "alice").</summary>
    public string Name { get; set; } = "";

    /// <summary>Base64-encoded Ed25519 public key. Used as the identity in signed envelopes.</summary>
    public string PublicKey { get; set; } = "";

    /// <summary>Base64-encoded Ed25519 private key. Marked [Sensitive] — excluded from output serialization.</summary>
    [Sensitive]
    public string PrivateKey { get; set; } = "";

    /// <summary>Whether this is the active default identity for the system actor.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Whether this identity has been soft-deleted. Archived identities are excluded from list results.</summary>
    public bool IsArchived { get; set; }

    /// <summary>When this identity was created (UTC).</summary>
    public DateTimeOffset Created { get; set; }

    /// <summary>
    /// String context returns the public key — %MyIdentity% in a string gives the public key.
    /// </summary>
    public override string ToString() => PublicKey;
}
