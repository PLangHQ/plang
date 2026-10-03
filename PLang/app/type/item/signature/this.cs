namespace app.type.item.signature;

using IWriter = global::app.type.format.IWriter;
using text = global::app.type.item.text.@this;
using datetime = global::app.type.item.datetime.@this;
using binary = global::app.type.item.binary.@this;
using hash = global::app.module.crypto.type.hash.@this;

/// <summary>
/// PLang <c>signature</c> value — the cryptographic-attestation <b>layer</b> that
/// wraps a Data. A signed value is not a Data with a sidecar <c>signature</c>
/// property; it is a <c>signature</c> layer whose <c>value</c> slot holds the
/// inner schema (the <c>data</c> being attested). Its self-describing wire form
/// is flat:
/// <code>
/// { "@schema":"signature", "type":"ed25519", "nonce":"…", "created":"…",
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
[global::app.Attributes.PlangType("signature")]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Shape => "object";

    /// <summary>The inner schema this signature attests — the <c>value</c> slot.</summary>
    public global::app.data.@this Value { get; }

    /// <summary>Signing algorithm — the layer's <c>algorithm</c> wire field (<c>ed25519</c> default).</summary>
    public text Algorithm { get; }

    /// <summary>Per-signature nonce (replay defence).</summary>
    public text Nonce { get; }

    /// <summary>When the signature was minted.</summary>
    public datetime Created { get; }

    // The expiry the signer signed — null is a permanent attestation. What the wire carries and the signing
    // bytes cover, unchanged by any reader.
    private readonly datetime? _expires;

    // How long after Created a signature read live off the wire is good, given at birth by its reader with its
    // origin; it counts only for a live one.
    private readonly global::app.type.item.duration.@this? _window;

    /// <summary>Where the signature came from — <c>%x!signature.origin%</c>: just signed, read live off the wire, or
    /// read from plang's own store. Born with it; it decides the window, the nonce check and the hash's view.</summary>
    public global::app.type.item.choice.@this<global::app.type.item.signature.Origin> Origin { get; }

    /// <summary>When the signature stops being good — <c>%x!signature.expires%</c>: the expiry the signer signed,
    /// or, for a signature read live, its window after <see cref="Created"/> if that comes first. Null when
    /// neither ends it.</summary>
    public datetime? Expires
    {
        get
        {
            if (Origin.Value != global::app.type.item.signature.Origin.Live || _window is not { } window) return _expires;
            var live = Created.Value + (System.TimeSpan)window;
            return _expires is { } signed && signed.Value <= live ? signed : new datetime(live);
        }
    }

    /// <summary>Whether the signature is past its <see cref="Expires"/> by the clock <paramref name="context"/>
    /// reads — <c>%x!signature.expired%</c>.</summary>
    public async System.Threading.Tasks.ValueTask<bool> Expired(global::app.actor.context.@this context)
    {
        if (Expires is not { } expires) return false;
        var now = await (await context.Variable.Get("NowUtc")).Clr<System.DateTimeOffset>(System.DateTimeOffset.UtcNow);
        return now > expires.Value;
    }

    /// <summary>One step down: <c>expired</c> asks the clock (<see cref="Expired"/>); every other member as any
    /// item answers it.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => string.Equals(key, "expired", System.StringComparison.OrdinalIgnoreCase) && parent.Context is { } context
            ? new global::app.data.@this(key, await Expired(context), parent: parent)
            : await base.Get(parent, key);

    /// <summary>The signing identity (public-key name).</summary>
    public text Identity { get; }

    /// <summary>Contracts asserted by this signature (e.g. <c>["C0"]</c>).</summary>
    public global::app.type.item.list.@this? Contracts { get; }

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
        global::app.type.item.list.@this? contracts = null,
        global::app.type.item.signature.Origin origin = global::app.type.item.signature.Origin.Signed,
        global::app.type.item.duration.@this? window = null)
    {
        Origin = origin;
        Value = value;
        Algorithm = algorithm ?? new text("ed25519");
        Nonce = nonce ?? new text("");
        Created = created;
        Identity = identity ?? new text("");
        Hash = hash;
        Signature = signature ?? new binary(System.Array.Empty<byte>());
        _expires = expires;
        Contracts = contracts;
        _window = window;
    }

    /// <summary>A copy of this layer with the signature bytes filled in — used by
    /// the signing module after it signs <see cref="ToSigningBytes"/> (the layer is
    /// immutable; build the unsigned form, sign, then stamp the bytes).</summary>
    public @this Signed(binary signature)
        => new(Value, Algorithm, Nonce, Created, Identity, Hash, signature, _expires, Contracts, Origin.Value, _window);

    protected internal override global::app.type.@this Type
        => new("signature", typeof(@this), Algorithm.ToString());

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
    /// layer object. The layer owns this layout; every leaf field renders ITSELF via
    /// its own <see cref="global::app.type.item.@this.Write"/> (text→String,
    /// datetime→DateTimeOffset, binary→Bytes), and the <c>value</c> slot recurses
    /// through <see cref="global::app.data.@this.Output"/> so the inner Data writes
    /// itself as a layer-level <c>@schema:"data"</c> record.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask Output(
        IWriter w, global::app.View mode, global::app.actor.context.@this? context)
    {
        w.BeginObject();
        w.Name(global::app.data.@this.WireSchema); w.String(WireSchemaSignature);
        // `type` = the algorithm within this layer — uniform with every layer
        // ({@schema:archive, type:gzip}, {@schema:encryption, type:aes-…}).
        w.Name("type"); Algorithm.Write(w);
        w.Name("nonce"); Nonce.Write(w);
        w.Name("created"); Created.Write(w);
        if (_expires is { } exp) { w.Name("expires"); exp.Write(w); }
        w.Name("identity"); Identity.Write(w);
        // Contracts are bare strings on the wire (and in ToSigningBytes), not
        // data records — emit the strings directly.
        if (Contracts is not null)
        {
            w.Name("contracts");
            w.BeginArray(-1);
            foreach (var c in ContractStrings()) w.String(c);
            w.EndArray();
        }
        // hash sub-object {type, value} — read straight off the typed hash.
        w.Name("hash");
        w.BeginObject();
        w.Name("type"); w.String(Hash.Algorithm?.Name ?? "");
        w.Name("value"); w.String(Hash.ToBase64());
        w.EndObject();
        w.Name("signature"); Signature.Write(w);
        // The inner Data is a full layer-level payload (its own @schema:data).
        w.Name("value"); await Value.Output(w, mode, context, layer: true);
        w.EndObject();
    }

    /// <summary>
    /// Sync layer render for the STJ <see cref="global::app.data.Wire"/> write paths
    /// (Serialize/Store/.pr) that cannot await <see cref="Output"/>. Same layout; the
    /// inner <c>value</c> rides via the sync tree-walk. Retired once every write path
    /// is on <see cref="Output"/> (then this and IWriter.Value both go).
    /// </summary>
    public override void Write(IWriter w)
    {
        w.BeginObject();
        w.Name(global::app.data.@this.WireSchema); w.String(WireSchemaSignature);
        w.Name("type"); Algorithm.Write(w);
        w.Name("nonce"); Nonce.Write(w);
        w.Name("created"); Created.Write(w);
        if (_expires is { } exp) { w.Name("expires"); exp.Write(w); }
        w.Name("identity"); Identity.Write(w);
        if (Contracts is not null)
        {
            w.Name("contracts");
            w.BeginArray(-1);
            foreach (var c in ContractStrings()) w.String(c);
            w.EndArray();
        }
        w.Name("hash");
        w.BeginObject();
        w.Name("type"); w.String(Hash.Algorithm?.Name ?? "");
        w.Name("value"); w.String(Hash.ToBase64());
        w.EndObject();
        w.Name("signature"); Signature.Write(w);
        w.Name("value"); w.Value(Value);
        w.EndObject();
    }

    /// <summary>The <c>@schema</c> value identifying a signature layer.</summary>
    public const string WireSchemaSignature = "signature";
}
