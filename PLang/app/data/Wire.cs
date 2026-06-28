using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.data;

using type = global::app.type.@this;

/// <summary>
/// Wire converter for <c>app.data.@this</c> — the single point where the
/// canonical five-field shape <c>{name, type, value, properties, signature}</c>
/// is emitted and parsed. The <c>properties</c> field is omitted when empty;
/// <c>signature</c> is omitted when null. The <c>value</c> slot is built by
/// <c>data.Normalize(View) → IWriter</c> so domain types ride as
/// <c>[Out]</c>-tagged property bags; no per-type JsonConverter is needed.
///
/// <para>
/// Sign-if-missing: each Data the converter visits during a Write walk gets
/// <see cref="@this.EnsureSigned"/> fired before emission, idempotently — a
/// Data that already carries a Signature is left alone. Forwarding chains
/// preserve provenance because inner Datas that arrived already-signed never
/// re-sign; new Datas in a freshly-wrapped outer get their own signature.
/// </para>
///
/// <para>
/// Hash carve-out: when crypto.Hash needs to canonicalize a Data D for
/// signing, the converter is told (via <see cref="MarkOuterForHash"/>) that
/// D is the "outer being signed right now." For that one Data, Write emits
/// <c>{name, type, value, properties}</c> with <em>no</em> signature field
/// and does NOT call EnsureSigned (which would loop). All inner Datas reached
/// through the walk still go through sign-if-missing and emit their full
/// shape, so the outer signature transitively binds inner attestations.
/// </para>
/// </summary>
public class Wire : JsonConverter<@this>
{
    // Ref-counted "outer being hashed right now" tracking. A plain HashSet
    // would lose the marker when nested Hash calls run on the same Data
    // instance (signing.verify re-hashing inner while outer Verify is mid-
    // serialise; chained crypto.Hash through a goal whose own result is
    // being signed): inner-disposal would Remove the Data the outer still
    // depended on, and the next outer Write would think it was safe to
    // EnsureSigned, causing recursion. Per-instance ref-count keeps each
    // scope's lifetime independent.
    private static readonly AsyncLocal<Dictionary<@this, int>?> _hashOuter = new();

    /// <summary>
    /// The view this converter emits in. Owned per-instance — the plang
    /// serializer keeps separate <see cref="System.Text.Json.JsonSerializerOptions"/>
    /// for outbound (<see cref="global::app.View.Out"/>) and store
    /// (<see cref="global::app.View.Store"/>) paths, each carrying its own
    /// converter. Treating this as data on the converter (rather than
    /// AsyncLocal ambient state) keeps the storage-vs-wire decision visible
    /// at the construction site.
    /// </summary>
    public global::app.View View { get; }

    /// <summary>
    /// Whether sign-if-missing fires during the Write walk. True for every wire
    /// that crosses an actor boundary (the default). False for the snapshot
    /// durable-execution wire: a snapshot is internal in-process state replayed
    /// into the same actor, so signing it is both unnecessary and a side effect
    /// (it mutates the captured Data and needs writable identity I/O, which is
    /// absent headless). Read is unaffected — signatures already present still
    /// rehydrate.
    /// </summary>
    public bool Sign { get; }

    // The actor context a lazily-deferred value slot carries so it can
    // materialize through the reader registry on touch. Null for the context-less
    // fallback (hashing, headless) — a deferred Data then materializes via the
    // type's own Convert. Owned per-instance, like View, set at the per-actor
    // serializer's construction site.
    private readonly actor.context.@this? _context;

    // The authored-content mode threaded into every value read this converter
    // drives — "plang" only on the dedicated goal/.pr-load Wire (a %ref% leaf borns
    // a live template), null everywhere else (runtime ingest borns literal). The
    // trust rides the reader instance, set once at the construction site, never
    // inferred from the bytes. Owned per-instance, like View/Sign.
    private readonly string? _template;

    public Wire() : this(global::app.View.Out) { }

    public Wire(global::app.View view, bool sign = true, actor.context.@this? context = null,
        string? template = null)
    {
        View = view;
        Sign = sign;
        _context = context;
        _template = template;
    }

    /// <summary>
    /// Marks <paramref name="data"/> as the "outer being hashed right now."
    /// While the returned scope is alive, the converter writes that one
    /// Data's body without invoking <see cref="@this.EnsureSigned"/> and
    /// without emitting its Signature field. Reference-counted: nested
    /// MarkOuterForHash calls on the same Data instance compose correctly.
    /// </summary>
    public static IDisposable MarkOuterForHash(@this data)
    {
        var map = _hashOuter.Value ??= new Dictionary<@this, int>(ReferenceEqualityComparer.Instance);
        map.TryGetValue(data, out var n);
        map[data] = n + 1;
        return new Scope(data);
    }

    private static bool IsHashOuter(@this data)
    {
        var map = _hashOuter.Value;
        return map != null && map.TryGetValue(data, out var n) && n > 0;
    }

    private sealed class Scope : IDisposable
    {
        private readonly @this _data;
        private bool _disposed;
        public Scope(@this data) { _data = data; }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var map = _hashOuter.Value;
            if (map == null) return;
            if (map.TryGetValue(_data, out var n))
            {
                if (n <= 1) map.Remove(_data);
                else map[_data] = n - 1;
            }
            if (map.Count == 0) _hashOuter.Value = null;
        }
    }

    // Hard ceiling on nested Data depth. STJ's own MaxDepth=64 caps a single
    // ParseValue call, but LiftDataIfShaped restarts STJ via
    // Deserialize<@this>(string, options) on each recursion — that resets
    // STJ's depth counter to zero, leaving only the C# call stack to bound
    // recursion (security v1 F1: pre-auth StackOverflow DoS at ~500 levels,
    // ~11 KB payload). The AsyncLocal counter below threads through every
    // Read invocation so the budget applies cumulatively across the
    // GetRawText round-trip. 64 mirrors STJ's default; throws JsonException
    // past it so the catch in plang.@this.DeserializeAsync turns it into a
    // typed PlangDeserializeError 400 rather than a crash.
    private const int MaxReadDepth = 64;
    private static readonly AsyncLocal<int> _readDepth = new();

    /// <summary>
    /// Wire is the canonical Data envelope — owns the shape for the base
    /// <see cref="@this"/> and every typed subclass (<c>Data&lt;T&gt;</c>,
    /// <c>DynamicData</c>, etc.). Without this override STJ skips the
    /// converter on subclasses and falls back to its parameterized-ctor
    /// deserializer, which can't bind <c>(name, value, type, parent)</c>
    /// against the <c>{name, type, value, properties, signature}</c> wire.
    /// </summary>
    public override bool CanConvert(System.Type typeToConvert)
        => typeof(@this).IsAssignableFrom(typeToConvert);

    // STJ-driven read (top-level via the entry, and nested Data inside lists). STJ hands a
    // ref reader with no buffer, so a structured value slot must DOM (ReadBody, buffer null).
    public override @this Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        => ReadCore(ref reader, typeToConvert, options, buffer: null)!;

    // Entry read over OWNED bytes — a structured value slot is sliced from the buffer raw
    // (no DOM). The plang serializer's DeserializeAsync drives this; the bytes ARE the buffer.
    public @this? ReadBuffered(byte[] bytes, JsonSerializerOptions options)
    {
        var reader = new Utf8JsonReader(bytes);
        reader.Read();
        return ReadCore(ref reader, typeof(@this), options, bytes);
    }

    private @this? ReadCore(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options, byte[]? buffer)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for app.data.@this wire shape");

        if (_readDepth.Value >= MaxReadDepth)
            throw new JsonException(
                $"app.data.@this wire shape nested past MaxReadDepth ({MaxReadDepth}) — payload rejected to prevent stack overflow.");
        // @schema dispatch: probe a struct COPY of the reader for the first
        // property (the writer always emits @schema first). A `signature` layer
        // wraps + attests the inner data — buffer it, rebuild via FromWire, run
        // the verify action (auto-verify on read: a bad signature fails the read),
        // peel to the inner data. Only the rare signature object buffers; the
        // common `data` path leaves the original reader untouched for ReadBody.
        var probe = reader;
        probe.Read();
        if (probe.TokenType == JsonTokenType.PropertyName
            && probe.GetString() == @this.WireSchema)
        {
            probe.Read();
            if (probe.GetString() == global::app.type.signature.@this.WireSchemaSignature)
                return ReadSignatureLayer(ref reader, options);
        }

        _readDepth.Value++;
        try
        {
            var jr = new global::app.channel.serializer.json.Reader(reader, buffer);
            var bodyData = new global::app.data.reader.@this(_context, _template).Read(ref jr, options);
            reader = jr.Inner;
            // When the caller asked for a typed Data<T>, wrap the base body
            // into a Data<T> so STJ's cast to typeToConvert succeeds. The
            // typed Data<T>.Value getter handles the dict-to-T conversion
            // lazily through GetValue<T>.
            if (typeToConvert != typeof(@this) && typeof(@this).IsAssignableFrom(typeToConvert))
            {
                return WrapAsTyped(bodyData, typeToConvert);
            }
            return bodyData;
        }
        finally
        {
            _readDepth.Value--;
        }
    }

    // Reads a `signature` layer object: rebuild it, auto-verify (run the verify
    // action — a bad/expired/wrong-key signature fails the READ), peel to the
    // inner data. The peeled data carries the unwrapping actor's context.
    private @this ReadSignatureLayer(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var layer = global::app.type.signature.@this.FromWire(doc.RootElement, options);

        if (_context == null)
        {
            // Transport (Out) is the external attack surface: a signed payload that
            // arrives with no actor to verify against must not be unwrapped — peeling
            // it would return the inner as if verified. Fail closed.
            if (View != global::app.View.Store)
                return @this.FromError(new app.error.ServiceError(
                    "Cannot verify a signature layer without an actor context.",
                    "SignatureVerifyContextMissing", 400));

            // At-rest (Store) artifacts — settings and permission grants — are read by
            // the store without an actor context, so verify cannot run here; the stored
            // grant is trusted on read. Tampering an at-rest artifact requires local
            // filesystem write, i.e. actor-level access already. (At-rest verification
            // needs the actor context carried into the store read.)
            return layer.Value;
        }

        // The inner data is re-hashed during verify (canonicalized through the
        // wire), so it needs the actor context the same way the outer does.
        layer.Value.Context = _context;

        var carrier = @this.Ok(layer);
        carrier.Context = _context;
        // At-rest artifacts (the Store view — permission grants, identities)
        // re-present the same nonce on every read and outlive the wire-
        // freshness window by design; their signature's own Expires is the
        // only time bound. Transport reads (Out view) keep the freshness +
        // nonce-replay defence.
        var verifyAction = new app.module.signing.verify
        {
            Data = carrier,
            SkipFreshnessCheck = new @this<global::app.type.@bool.@this>(
                "", View == global::app.View.Store),
        };
        var verifyResult = _context.App
            .RunAction(verifyAction, _context)
            .GetAwaiter().GetResult();
        if (!verifyResult.Success)
            return @this.FromError(verifyResult.Error
                ?? new app.error.ServiceError("Signature verification failed", "SignatureInvalid", 400));

        var inner = layer.Value;
        inner.Context = _context;
        return inner;
    }

    private static @this WrapAsTyped(@this body, System.Type targetType)
    {
        // Data<T>'s ctor parameters all have C# defaults — but reflection
        // sees a 4-arg signature. Invoke it via Type.Missing for each slot
        // so the runtime applies the declared defaults, then copy body
        // state on top. Value stays raw (dict / list); Data<T>.Value's
        // GetValue<T>() converts lazily at read-time.
        var typed = (@this)System.Activator.CreateInstance(
            targetType,
            System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.OptionalParamBinding,
            null,
            new object?[] { System.Type.Missing, System.Type.Missing, System.Type.Missing, System.Type.Missing },
            null)!;
        typed.Name = body.Name;
        // The body's instance carries its own type/kind/chain — move it whole.
        typed.SetValueDirect(body.Instance);
        typed.Properties = body.Properties;
        return typed;
    }


    // Emits an untouched raw-backed value verbatim into the value slot, keeping
    // the slot valid json. Raw json (object/json) and number literals are already
    // json — write them raw (byte-identical passthrough). Any other raw string
    // (text, csv, xml, yaml) json-encodes as a string; raw bytes base64.
    private static void EmitRawVerbatim(Utf8JsonWriter writer, @this data)
    {
        var raw = data.Raw;
        if (raw is byte[] bytes) { writer.WriteBase64StringValue(bytes); return; }
        if (raw is string s)
        {
            var t = data.Type;
            bool isJson = (t.Name is "object" or "item" && string.Equals(t.Kind, "json", System.StringComparison.OrdinalIgnoreCase))
                          || t.Name == "number";
            if (isJson) writer.WriteRawValue(s);
            else writer.WriteStringValue(s);
            return;
        }
        // No raw of a known shape — fall back to null (should not happen: caller
        // gates on RawUntouched, which requires a non-null raw).
        writer.WriteNullValue();
    }

    public override void Write(Utf8JsonWriter writer, @this data, JsonSerializerOptions options)
    {
        var isHashOuter = IsHashOuter(data);
        // Sign at the I/O boundary: a Data crossing application/plang within a
        // real actor scope (Context.Actor non-null) is wrapped in a `signature`
        // layer — ONE layer over the whole payload. Skipped when: signing is off
        // (snapshot wire), no actor (internal serialise — test fixtures, .pr
        // authoring, raw IClass calls), the hash-canonicalization pass
        // (isHashOuter), or the value is already a layer (re-serialise / pre-signed).
        if (Sign && !isHashOuter && data.Context?.Actor != null
            && data.Peek() is not global::app.type.signature.@this)
        {
            var signResult = data.Context.App
                .RunAction(new app.module.signing.sign { Data = data }, data.Context)
                .GetAwaiter().GetResult();
            if (signResult.Success) data = signResult;
        }

        // A layer value is HOISTED to top level: write the layer object itself
        // ({@schema:signature, …, value:<inner record>}), not a data envelope
        // wrapping it. One hoist rule serves every @schema layer. The inner data
        // is written by the layer's own Write via json.Writer's nested-record
        // arm — no STJ recursion, so it is not re-signed.
        if (data.Peek() is global::app.type.signature.@this)
        {
            new app.channel.serializer.json.Writer(writer, options, View,
                renderers: data.Context?.App?.Type.Renderers).Value(data.Peek());
            return;
        }

        writer.WriteStartObject();
        // @schema:data — the marker that says this JSON object IS a Data. First key,
        // always present (signed: it's intrinsic wire identity, unlike name).
        writer.WriteString(@this.WireSchema, @this.WireSchemaData);
        // The variable name is a binding label, not part of the data's identity —
        // excluded from the signed hash (isHashOuter) so a value verifies the
        // same no matter which variable holds it, and excluded from the OUTBOUND
        // wire entirely: a server's binding label is not API surface, and a
        // client that used it would couple to a name the server may rename.
        // The Store view keeps it — .pr action parameters and local persistence
        // bind by name. The reader still accepts `name` from either form.
        if (!isHashOuter && View == global::app.View.Store)
            writer.WriteString("name", data.Name);

        // type — emit as the structured entity {name, kind?, strict?} via the
        // type's own JsonConverter. ONE field carrying the full identity; the
        // historical flat sibling-key shape (`type` string + `kind` string)
        // is gone. Null sentinel is skipped entirely so the wire stays compact.
        if (!data.Type.IsNull)
        {
            writer.WritePropertyName("type");
            JsonSerializer.Serialize(writer, data.Type, options);
        }

        writer.WritePropertyName("value");
        // Verbatim passthrough: an untouched raw-backed Data (read lazily, relayed
        // by a courier, never navigated) serializes its raw source form straight
        // back out — no materialize, no parse-then-reserialize. The value slot
        // must stay valid json, so raw json (object/json) and number literals emit
        // raw; other raw strings json-encode; raw bytes base64.
        // The value slot routes through Normalize + json.Writer. Domain
        // objects emit as their filtered property bag;
        // primitives, nested Data, and lists pass through unchanged. View
        // selects the filter:
        //   - View.Out (default) — third-party-facing, [Out] only, Sensitive
        //     excluded, Masked emits "****".
        //   - View.Store — local persistence (sqlite). [Store] only,
        //     Sensitive included, Masked ignored. Round-trips local state.
        //   - View.Debug — diagnostic dump.
        var jsonWriter = new app.channel.serializer.json.Writer(writer, options, View,
            renderers: data.Context?.App?.Type.Renderers);
        if (data.RawUntouched)
            EmitRawVerbatim(writer, data);
        else
            jsonWriter.Value(data.Normalize(View));

        // properties — nested object, omitted when empty to keep the wire compact.
        // Sign-if-missing walks Value-graph Datas only, never Properties; the
        // outer Data's signature covers the canonicalized wire (including this
        // nested object), so tampering with any Property still invalidates the
        // outer signature, but no per-Property attestations are conjured.
        if (data.Properties.Count > 0)
        {
            writer.WritePropertyName("properties");
            writer.WriteStartObject();
            foreach (var kvp in data.Properties)
            {
                writer.WritePropertyName(kvp.Key);
                // Route through Normalize + json.Writer same as the value slot,
                // so [Out] / [Sensitive] / [Masked] discipline applies symmetrically
                // if a caller deposits a domain object into Properties.
                var normalized = @this.NormalizeValue(kvp.Value, View,
                    new HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance),
                    depth: 0);
                jsonWriter.Value(normalized);
            }
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }
}
