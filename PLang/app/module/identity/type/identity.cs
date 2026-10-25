using app;
using app.Attributes;

namespace app.module.identity;

/// <summary>
/// Represents a PLang identity (key pair with metadata).
/// Plain domain class — wrapped in Data&lt;Identity&gt; by handlers.
/// Persistence is owned by IIdentity.
/// </summary>
[PlangType("identity")]
public sealed class Identity : global::app.type.item.@this, global::app.type.item.ICreate<Identity>
{
    /// <summary>An identity, born holding <paramref name="type"/> — the app's identity.</summary>
    public Identity(global::app.type.@this type) : base(type) { }
    public Identity(string name, global::app.type.@this type) : base(type) { Name = name; }

    /// <summary>Identity name (e.g., "default", "work").</summary>
    [LlmBuilder, Out, Store] public string Name { get; set; } = "Identity";

    /// <summary>The Ed25519 public key, URL-safe base64 (- and _, no =) — <c>%Identity%</c>, used as it is as a
    /// path segment, a URL part or a file name. Used as the identity in signed Datas. A key stored before keys
    /// were written URL-safe reads back in that form.</summary>
    [LlmBuilder, Out, Store] public string PublicKey
    {
        get => _publicKey;
        set => _publicKey = value.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
    private string _publicKey = "";

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

    // The identity is written as its tags say, through the base write: what crosses ({name, publicKey}), what is
    // kept (every member, the private key whole, for the local store's round trip), and a dump (every member, the
    // private key masked). Read back by ICreate<Identity>.Create from that object.

    /// <summary>
    /// String context returns the public key — %MyIdentity% in a string gives the public key.
    /// </summary>
    public override string ToString() => PublicKey;

    /// <summary>This identity as a step may hold it: every member but its private key, which stays in its owner's
    /// setting (signing reads it there; <c>identity.export</c> refuses until critical actions hand it out).</summary>
    internal Identity Public() => new(Name, Type)
    {
        PublicKey = PublicKey,
        IsDefault = IsDefault,
        IsArchived = IsArchived,
        Created = Created,
    };
}
