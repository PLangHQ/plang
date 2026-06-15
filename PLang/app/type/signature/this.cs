namespace app.type.signature;

using IWriter = global::app.channel.serializer.IWriter;
using text = global::app.type.text.@this;
using datetime = global::app.type.datetime.@this;
using binary = global::app.type.binary.@this;
using hash = global::app.module.crypto.type.hash.@this;

/// <summary>
/// PLang <c>signature</c> value — the cryptographic-attestation <b>layer</b> that
/// wraps a Data. A signed value is not a Data with a sidecar <c>signature</c>
/// property; it is a <c>signature</c> layer whose <c>value</c> slot holds the
/// inner schema (the <c>data</c> being attested). Its self-describing wire form
/// is flat:
/// <code>
/// { "@schema":"signature", "algorithm":"ed25519", "nonce":"…", "created":"…",
///   "identity":"…", "hash":{"type":"keccak256","value":"&lt;b64&gt;"},
///   "signature":"&lt;b64&gt;", "value":{ "@schema":"data", … } }
/// </code>
///
/// <para>OBP: the layer owns its <b>wire shape</b> (<see cref="Write"/> renders
/// the object via the <see cref="IWriter"/> object surface, each field rendering
/// ITSELF — Rule 9; the writer never type-switches on it). The cryptographic
/// <b>operation</b> (hash, sign, verify) is owned by the signing module, reached
/// at runtime via <c>App.Code.Get&lt;ISigning&gt;()</c> — never inlined here. The
/// signature is computed over the canonical bytes of the inner <c>value</c>;
/// because the inner data is a separate object, it hashes whole — no
/// exclude-self carve-out.</para>
///
/// <para>Born native: every field is a plang value type (<c>text</c>,
/// <c>datetime</c>, <c>binary</c>, <c>list</c>, <c>dict</c>, <c>hash</c>), not a
/// CLR primitive — each renders and behaves itself.</para>
/// </summary>
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "(signature)";
    public static string Shape => "object";

    /// <summary>The inner schema this signature attests — the <c>value</c> slot.</summary>
    public global::app.data.@this Value { get; }

    /// <summary>Signing algorithm — the layer's <c>algorithm</c> wire field (<c>ed25519</c> default).</summary>
    public text Algorithm { get; }

    /// <summary>Per-signature nonce (replay defence).</summary>
    public text Nonce { get; }

    /// <summary>When the signature was minted.</summary>
    public datetime Created { get; }

    /// <summary>Optional expiry — null is a permanent attestation.</summary>
    public datetime? Expires { get; }

    /// <summary>The signing identity (public-key name).</summary>
    public text Identity { get; }

    /// <summary>Contracts asserted by this signature (e.g. <c>["C0"]</c>).</summary>
    public global::app.type.list.@this? Contracts { get; }

    /// <summary>The digest the signature covers — the typed crypto hash (it owns
    /// its algorithm and bytes, so the module reads them off without a cast).</summary>
    public hash Hash { get; }

    /// <summary>The signature bytes over the digest (renders base64).</summary>
    public binary Signature { get; }

    public @this(
        global::app.data.@this value,
        text algorithm,
        text nonce,
        datetime created,
        text identity,
        hash hash,
        binary signature,
        datetime? expires = null,
        global::app.type.list.@this? contracts = null)
    {
        Value = value;
        Algorithm = algorithm ?? new text("ed25519");
        Nonce = nonce ?? new text("");
        Created = created;
        Identity = identity ?? new text("");
        Hash = hash;
        Signature = signature ?? new binary(System.Array.Empty<byte>());
        Expires = expires;
        Contracts = contracts;
    }

    protected internal override global::app.type.@this Mint()
        => new("signature", typeof(global::app.data.@this)) { Kind = Algorithm.ToString() };

    /// <summary>Structural — the inner value is a nested record, not a leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>A signature is always a present, truthy attestation.</summary>
    public override bool IsTruthy() => true;

    /// <summary>The CLR exit door hands back the attested inner Data.</summary>
    internal override object? Clr(System.Type target)
        => target.IsAssignableFrom(typeof(global::app.data.@this)) ? Value : ClrConvert(Value, target);

    public override string ToString() => $"signature({Algorithm}) over {Identity}";

    /// <summary>
    /// Renders the flat <c>{@schema:"signature", …fields…, value:&lt;inner&gt;}</c>
    /// layer object. The layer owns this layout; every field renders ITSELF
    /// through <see cref="IWriter.Value"/>, and the <c>value</c> slot recurses so
    /// the inner Data writes itself as a <c>@schema:"data"</c> record.
    /// </summary>
    public override void Write(IWriter w)
    {
        w.BeginObject();
        w.Name(global::app.data.@this.WireSchema); w.String(WireSchemaSignature);
        w.Name("algorithm"); w.Value(Algorithm);
        w.Name("nonce"); w.Value(Nonce);
        w.Name("created"); w.Value(Created);
        if (Expires is { } exp) { w.Name("expires"); w.Value(exp); }
        w.Name("identity"); w.Value(Identity);
        if (Contracts is not null) { w.Name("contracts"); w.Value(Contracts); }
        // hash sub-object {type, value} — read straight off the typed hash.
        w.Name("hash");
        w.BeginObject();
        w.Name("type"); w.String(Hash.Algorithm);
        w.Name("value"); w.String(Hash.ToBase64());
        w.EndObject();
        w.Name("signature"); w.Value(Signature);
        w.Name("value"); w.Value(Value);
        w.EndObject();
    }

    /// <summary>The <c>@schema</c> value identifying a signature layer.</summary>
    public const string WireSchemaSignature = "signature";
}
