using app;
using app.Attributes;

namespace app.module.identity;

/// <summary>
/// Represents a PLang identity (key pair with metadata).
/// Plain domain class — wrapped in Data&lt;Identity&gt; by handlers.
/// Persistence is owned by IIdentity.
/// </summary>
[PlangType]
public sealed class Identity : global::app.type.item.@this, global::app.type.item.ICreate<Identity>
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
    /// The identity owns its wire form (OBP Rule 9 — the writer never type-switches
    /// on it). View selects the field set, symmetric with the [Out]/[Store] tags:
    ///   Out   → the public face {name, publicKey}
    ///   Store → adds the [Store]/[Sensitive] fields {privateKey, isDefault,
    ///           isArchived, created} for local sqlite round-trip (real PrivateKey).
    ///   Debug → same fields as Store, but PrivateKey masked to "***".
    /// Read-back is ICreate&lt;Identity&gt;.Create from this object.
    /// </summary>
    public override System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter w, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        w.BeginObject();
        w.Name("name");      w.String(Name);        // [Out, Store]
        w.Name("publicKey"); w.String(PublicKey);   // [Out, Store]
        if (mode == global::app.View.Store || mode == global::app.View.Debug)
        {
            // [Sensitive, Store] — real value only at rest (Store); masked in Debug.
            w.Name("privateKey");
            w.String(mode == global::app.View.Store ? PrivateKey : "***");
            w.Name("isDefault");  w.Bool(IsDefault);             // [Store]
            w.Name("isArchived"); w.Bool(IsArchived);            // [Store]
            w.Name("created");    w.DateTimeOffset(Created);     // [Store]
        }
        w.EndObject();
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }

    /// <summary>
    /// String context returns the public key — %MyIdentity% in a string gives the public key.
    /// </summary>
    public override string ToString() => PublicKey;
}
