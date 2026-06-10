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

    public Wire() : this(global::app.View.Out) { }

    public Wire(global::app.View view, bool sign = true, actor.context.@this? context = null)
    {
        View = view;
        Sign = sign;
        _context = context;
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

    public override @this Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null!;
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for app.data.@this wire shape");

        if (_readDepth.Value >= MaxReadDepth)
            throw new JsonException(
                $"app.data.@this wire shape nested past MaxReadDepth ({MaxReadDepth}) — payload rejected to prevent stack overflow.");
        _readDepth.Value++;
        try
        {
            var bodyData = ReadBody(ref reader, options);
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

    private static void EnsureInnerSigned(object? value)
    {
        if (value is null) return;
        if (value is @this inner)
        {
            if (inner.Signature == null && inner.Context?.Actor != null)
                inner.EnsureSigned();
            EnsureInnerSigned(inner.Peek());
            return;
        }
        // Native dict: its entries ARE Data — recurse into each so a signed value
        // held under a key gets sealed before any byte leaves.
        if (value is app.type.dict.@this nativeDict)
        {
            foreach (var entry in nativeDict.Entries) EnsureInnerSigned(entry);
            return;
        }
        // IDictionary's IEnumerable yields DictionaryEntry boxes, not values —
        // foreach over the dict would walk DictionaryEntry which is neither
        // Data nor IEnumerable, and inner Datas held as dict values would
        // never get sealed. Branch on IDictionary first.
        if (value is System.Collections.IDictionary dict)
        {
            foreach (var v in dict.Values) EnsureInnerSigned(v);
            return;
        }
        if (value is System.Collections.IEnumerable seq && value is not string)
        {
            foreach (var item in seq) EnsureInnerSigned(item);
        }
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
        typed.SetValue(body.Materialize());
        if (body.Type != null) typed.Type = body.Type;
        typed.Properties = body.Properties;
        if (body.Signature != null) typed.Signature = body.Signature;
        return typed;
    }

    private @this ReadBody(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        string name = "";
        object? value = null;
        type? typeRef = null;
        app.module.signing.Signature? signature = null;
        Properties? properties = null;
        string? deferredRaw = null;   // set when the value slot is captured for lazy materialization

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (deferredRaw != null && typeRef != null)
                {
                    // Lazy value slot: a shape-typed value (object/table) rides as
                    // its raw source form and materializes on first touch through
                    // the reader — verbatim passthrough for untouched relay Data.
                    var lazy = @this.FromRaw(deferredRaw, typeRef, _context);
                    lazy.Name = name;
                    if (signature != null) lazy.Signature = signature;
                    if (properties != null) lazy.Properties = properties;
                    return lazy;
                }
                var data = new @this(name, value, typeRef);
                if (signature != null) data.Signature = signature;
                if (properties != null) data.Properties = properties;
                return data;
            }
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected PropertyName inside app.data.@this wire shape");
            var key = reader.GetString()!;
            reader.Read();
            switch (key.ToLowerInvariant())
            {
                case "name":
                    name = reader.TokenType == JsonTokenType.Null ? "" : reader.GetString() ?? "";
                    break;
                case "type":
                    // The structured entity {name, kind?, strict?} — its own
                    // JsonConverter handles both string-form ("text") and
                    // dict-form. No sibling `kind` key on the wire — the
                    // entity owns its full identity in one slot.
                    if (reader.TokenType == JsonTokenType.Null) typeRef = null;
                    else typeRef = JsonSerializer.Deserialize<type>(ref reader, options);
                    break;
                case "value":
                    // Defer a shape-typed value (object/table) — capture its raw
                    // source form and let it materialize lazily on first touch.
                    // The type slot precedes value on the wire, so typeRef is known
                    // here. A json-string token unwraps to its content (the text a
                    // text-shaped reader expects); object/array/number keep their
                    // raw json. Scoped to object/table so scalars, domain objects,
                    // and dict<…>-typed values keep their eager path unchanged.
                    if (typeRef != null && IsDeferrableShape(typeRef))
                    {
                        using var vdoc = JsonDocument.ParseValue(ref reader);
                        var el = vdoc.RootElement;
                        deferredRaw = el.ValueKind == JsonValueKind.String
                            ? el.GetString() ?? ""
                            : el.GetRawText();
                    }
                    // Peek at the token to see whether the value slot is a
                    // nested Data (recognised by its {name, value, [signature]}
                    // shape) — STJ alone would surface a Dictionary because the
                    // destination type is object. Without rehydration the inner
                    // Data's Signature would be observable as a sub-dictionary
                    // but never reach signing.verify or App-level navigation.
                    else if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        // Buffer the sub-object so we can decide.
                        using var doc = JsonDocument.ParseValue(ref reader);
                        value = LiftDataIfShaped(doc.RootElement, options);
                    }
                    else if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        // An array value is the native list shape on the wire — every
                        // element self-describes as a Data envelope. Lift each back to a
                        // Data (a signed element regains its Signature); collections hold
                        // Data, so the list value type holds the reconstructed elements.
                        using var doc = JsonDocument.ParseValue(ref reader);
                        value = LiftArrayElements(doc.RootElement, options);
                    }
                    else
                    {
                        value = JsonSerializer.Deserialize<object?>(ref reader, options);
                    }
                    break;
                case "signature":
                    signature = JsonSerializer.Deserialize<app.module.signing.Signature>(ref reader, options);
                    break;
                case "properties":
                    properties = ReadPropertiesObject(ref reader);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        throw new JsonException("Unterminated app.data.@this wire shape");
    }

    // Shape-typed values that read themselves from a raw *encoded* source form (a
    // tree or a grid). Requires a real encoding kind (json/xml/csv/…): a bare
    // {object} with no kind is a nested Data envelope (Data's PLang name is
    // "object"), which LiftDataIfShaped must rehydrate — not a raw payload to
    // defer. Scalars/domain/dict<…> values also stay eager. Narrow, additive.
    private static bool IsDeferrableShape(type t)
        => t.Name is "object" or "item" or "table" && !string.IsNullOrEmpty(t.Kind);

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

    private static Properties ReadPropertiesObject(ref Utf8JsonReader reader)
    {
        var props = new Properties();
        if (reader.TokenType == JsonTokenType.Null) return props;
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("properties field must be a JSON object");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) return props;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected property name inside properties object");
            var key = reader.GetString()!;
            reader.Read();
            object? value = ReadPropertyPrimitive(ref reader);
            props[key] = value;
        }
        throw new JsonException("Unterminated properties object");
    }

    private static object? ReadPropertyPrimitive(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null: return null;
            case JsonTokenType.String: return reader.GetString();
            case JsonTokenType.True: return true;
            case JsonTokenType.False: return false;
            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var l)) return l;
                // A bare decimal-point literal defaults to double (universal language
                // convention); decimal is opt-in via `as number/decimal`.
                return reader.GetDouble();
            case JsonTokenType.StartArray:
            {
                var list = new List<object?>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    list.Add(ReadPropertyPrimitive(ref reader));
                return list;
            }
            case JsonTokenType.StartObject:
            {
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    var key = reader.GetString()!;
                    reader.Read();
                    dict[key] = ReadPropertyPrimitive(ref reader);
                }
                return dict;
            }
            default:
                throw new JsonException($"Unexpected token in property value: {reader.TokenType}");
        }
    }

    // A value-slot object is a Data iff it carries the @schema:data marker (every Data
    // writes it). The explicit marker replaced the old "has both name and value keys"
    // shape-sniff — a user map that happens to have name/value/type keys but no marker
    // stays a plain map, unambiguously, with no value-graph guessing.
    private static object? LiftDataIfShaped(System.Text.Json.JsonElement element, JsonSerializerOptions options)
    {
        // A value-slot object is a Data strictly when it carries the @schema:data
        // marker (every Data writes it). No shape-sniffing: a user map with name/value
        // keys but no marker deserializes as a plain object, not lifted to a Data.
        if (!HasDataMarker(element))
            return element.Deserialize<object?>(options);

        return element.Deserialize<@this>(options);
    }

    // Reconstructs an array value slot into the native list value type. Every element
    // on the wire self-describes as a Data (the writer's list arm marks each with
    // @schema:data), so a marked element lifts back to a Data (regaining its Signature);
    // anything else is wrapped as a bare element Data. The marker recognizes even a
    // typeless element (the case the old name+value sniff existed for).
    private static app.type.list.@this LiftArrayElements(System.Text.Json.JsonElement array, JsonSerializerOptions options)
    {
        var list = new app.type.list.@this();
        foreach (var el in array.EnumerateArray())
        {
            if (HasDataMarker(el))
                list.Add(el.Deserialize<@this>(options)!);
            else
                list.Add(new @this("", @this.UnwrapJsonElement(el)));
        }
        return list;
    }

    // A JSON object IS a Data iff it carries the @schema:data marker — the one strict,
    // unambiguous recognizer for both the value slot (LiftDataIfShaped) and array
    // elements (LiftArrayElements). Replaces the name+value shape heuristic.
    private static bool HasDataMarker(System.Text.Json.JsonElement element)
        => element.ValueKind == System.Text.Json.JsonValueKind.Object
           && element.TryGetProperty(@this.WireSchema, out var s)
           && s.ValueKind == System.Text.Json.JsonValueKind.String
           && s.GetString() == @this.WireSchemaData;

    public override void Write(Utf8JsonWriter writer, @this data, JsonSerializerOptions options)
    {
        var isHashOuter = IsHashOuter(data);
        // Sign-if-missing — but only when the Data is wired into a real actor
        // scope (Context.Actor is non-null). A bare Context with no Actor is
        // the "internal in-memory serialise" case (test fixtures, .pr
        // authoring, raw IClass calls); signing has no identity to draw on
        // and skipping silently keeps those paths working. Production
        // discipline (Variables.Set / Channels.Register) always sets Actor
        // before any wire crossing.
        if (Sign && !isHashOuter && data.Signature == null && data.Context?.Actor != null)
        {
            data.EnsureSigned();
        }

        // Inner Datas in the value graph are emitted inline by json.Writer
        // (no STJ recursion), so the sign-if-missing check above only fires
        // for the outer. Walk the graph once here so every Data in scope
        // gets sealed before any byte leaves. Skip an untouched raw-backed Data:
        // its value is raw text/bytes (no nested Data to seal), and reading
        // .Value would materialize it and break verbatim passthrough.
        if (Sign && !data.RawUntouched) EnsureInnerSigned(data.Peek());

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

        if (!isHashOuter && data.Signature != null)
        {
            writer.WritePropertyName("signature");
            JsonSerializer.Serialize(writer, data.Signature, options);
        }

        writer.WriteEndObject();
    }
}
