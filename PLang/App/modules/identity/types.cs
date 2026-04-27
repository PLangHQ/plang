using App;
using App.Attributes;

namespace App.modules.identity;

/// <summary>
/// Represents a PLang identity (key pair with metadata).
/// Plain domain class — wrapped in Data&lt;Identity&gt; by handlers.
/// Persistence is owned by IIdentityProvider.
/// </summary>
[PlangType("identity")]
public sealed class Identity
{
    public Identity() { }
    public Identity(string name) { Name = name; }

    /// <summary>Identity name (e.g., "default", "work").</summary>
    [LlmBuilder] public string Name { get; set; } = "Identity";

    /// <summary>Base64-encoded Ed25519 public key. Used as the identity in signed envelopes.</summary>
    [LlmBuilder] public string PublicKey { get; set; } = "";

    /// <summary>Base64-encoded Ed25519 private key. Marked [Sensitive] — excluded from output serialization.</summary>
    [Sensitive]
    public string PrivateKey { get; set; } = "";

    /// <summary>Whether this is the active default identity for the system actor.</summary>
    [LlmBuilder] public bool IsDefault { get; set; }

    /// <summary>Whether this identity has been soft-deleted. Archived identities are excluded from list results.</summary>
    [LlmBuilder] public bool IsArchived { get; set; }

    /// <summary>When this identity was created (UTC).</summary>
    [LlmBuilder] public DateTimeOffset Created { get; set; }

    /// <summary>
    /// String context returns the public key — %MyIdentity% in a string gives the public key.
    /// </summary>
    public override string ToString() => PublicKey;
}
