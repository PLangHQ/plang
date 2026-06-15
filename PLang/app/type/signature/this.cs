namespace app.type.signature;

using IWriter = global::app.channel.serializer.IWriter;

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
/// the object via the <see cref="IWriter"/> object surface; the writer never
/// type-switches on it — Rule 9). The cryptographic <b>operation</b> (hash, sign,
/// verify) is owned by the signing module, reached at runtime via
/// <c>App.Code.Get&lt;ISigning&gt;()</c> — never inlined here. The signature is
/// computed over the canonical bytes of the inner <c>value</c>; because the inner
/// data is a separate object, it hashes whole — no exclude-self carve-out.</para>
/// </summary>
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "(signature)";
    public static string Shape => "object";

    /// <summary>The inner schema this signature attests — the <c>value</c> slot.</summary>
    public global::app.data.@this Value { get; }

    /// <summary>Signing algorithm — the layer's <c>algorithm</c> wire field (<c>ed25519</c> default).</summary>
    public string Algorithm { get; }

    /// <summary>Per-signature nonce (replay defence).</summary>
    public string Nonce { get; }

    /// <summary>When the signature was minted.</summary>
    public System.DateTimeOffset Created { get; }

    /// <summary>Optional expiry — null is a permanent attestation.</summary>
    public System.DateTimeOffset? Expires { get; }

    /// <summary>The signing identity (public-key name).</summary>
    public string Identity { get; }

    /// <summary>Contracts asserted by this signature (e.g. <c>["C0"]</c>).</summary>
    public System.Collections.Generic.IReadOnlyList<string>? Contracts { get; }

    /// <summary>Optional headers carried by the signature.</summary>
    public System.Collections.Generic.IReadOnlyDictionary<string, object>? Headers { get; }

    /// <summary>The digest the signature covers — the typed crypto hash (it owns
    /// its algorithm and bytes, so the module reads them off without a cast).</summary>
    public global::app.module.crypto.type.hash.@this Hash { get; }

    /// <summary>Base64 signature bytes over the digest.</summary>
    public string Sig { get; }

    public @this(
        global::app.data.@this value,
        string algorithm,
        string nonce,
        System.DateTimeOffset created,
        string identity,
        global::app.module.crypto.type.hash.@this hash,
        string sig,
        System.DateTimeOffset? expires = null,
        System.Collections.Generic.IReadOnlyList<string>? contracts = null,
        System.Collections.Generic.IReadOnlyDictionary<string, object>? headers = null)
    {
        Value = value;
        Algorithm = string.IsNullOrEmpty(algorithm) ? "ed25519" : algorithm;
        Nonce = nonce ?? "";
        Created = created;
        Identity = identity ?? "";
        Hash = hash;
        Sig = sig ?? "";
        Expires = expires;
        Contracts = contracts;
        Headers = headers;
    }

    protected internal override global::app.type.@this Mint()
        => new("signature", typeof(global::app.data.@this)) { Kind = Algorithm };

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
    /// layer object. The layer owns this layout; the <c>value</c> slot recurses
    /// through <see cref="IWriter.Value"/> so the inner Data writes ITSELF as a
    /// <c>@schema:"data"</c> record.
    /// </summary>
    public override void Write(IWriter w)
    {
        w.BeginObject();
        w.Name(global::app.data.@this.WireSchema); w.String(WireSchemaSignature);
        w.Name("algorithm"); w.String(Algorithm);
        w.Name("nonce"); w.String(Nonce);
        w.Name("created"); w.DateTimeOffset(Created);
        if (Expires is { } exp) { w.Name("expires"); w.DateTimeOffset(exp); }
        w.Name("identity"); w.String(Identity);
        if (Contracts is { Count: > 0 })
        {
            w.Name("contracts");
            w.BeginArray(Contracts.Count);
            foreach (var c in Contracts) w.String(c);
            w.EndArray();
        }
        // hash sub-object {type, value} — read straight off the typed hash.
        w.Name("hash");
        w.BeginObject();
        w.Name("type"); w.String(Hash.Algorithm);
        w.Name("value"); w.String(Hash.ToBase64());
        w.EndObject();
        if (Headers is { Count: > 0 })
        {
            w.Name("headers");
            w.BeginObject();
            foreach (var kv in Headers) { w.Name(kv.Key); w.Value(kv.Value); }
            w.EndObject();
        }
        w.Name("signature"); w.String(Sig);
        w.Name("value"); w.Value(Value);
        w.EndObject();
    }

    /// <summary>The <c>@schema</c> value identifying a signature layer.</summary>
    public const string WireSchemaSignature = "signature";
}
