using app;
using app.Attributes;

namespace app.module.identity;

/// <summary>
/// Represents a PLang identity (key pair with metadata).
/// Plain domain class — wrapped in Data&lt;Identity&gt; by handlers.
/// Persistence is owned by IIdentity.
/// </summary>
[PlangType]
public sealed class Identity
{
    public Identity() { }
    public Identity(string name) { Name = name; }

    /// <summary>Identity name (e.g., "default", "work").</summary>
    [LlmBuilder, Out, Store] public string Name { get; set; } = "Identity";

    /// <summary>Base64-encoded Ed25519 public key. Used as the identity in signed Datas.</summary>
    [LlmBuilder, Out, Store] public string PublicKey { get; set; } = "";

    /// <summary>Base64-encoded Ed25519 private key. Marked [Sensitive] — excluded from output serialization.
    /// [Store] so it round-trips local sqlite persistence (signing needs it on re-read).</summary>
    [Sensitive, Store]
    public string PrivateKey { get; set; } = "";

    /// <summary>Whether this is the active default identity for the system actor.</summary>
    [LlmBuilder, Store] public bool IsDefault { get; set; }

    /// <summary>Whether this identity has been soft-deleted. Archived identities are excluded from list results.</summary>
    [LlmBuilder, Store] public bool IsArchived { get; set; }

    /// <summary>When this identity was created (UTC).</summary>
    [LlmBuilder, Store] public DateTimeOffset Created { get; set; }

    /// <summary>
    /// String context returns the public key — %MyIdentity% in a string gives the public key.
    /// </summary>
    public override string ToString() => PublicKey;
}
