using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.data;

using type = global::app.type.@this;

/// <summary>
/// Wire converter for <c>app.data.@this</c> — the single point where the
/// canonical five-field shape <c>{name, type, value, properties, signature}</c>
/// is emitted and parsed. The <c>properties</c> field is omitted when empty;
/// <c>signature</c> is omitted when null. The <c>value</c> slot is written by the value's own
/// <c>Output</c> (each type emits its form; structural types reflect their <c>[Out]</c>-tagged
/// properties via <c>OutputTagged</c>); no per-type JsonConverter is needed.
/// Read-only: a Data writes itself via <c>Data.Output</c> (signing lives there), so this
/// converter only reads — its <c>Write</c> throws.
/// </summary>
public class Wire : JsonConverter<@this>
{
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
    private readonly actor.context.@this _context;

    // The authored-content mode threaded into every value read this converter
    // drives — "plang" only on the dedicated goal/.pr-load Wire (a %ref% leaf borns
    // a live template), null everywhere else (runtime ingest borns literal). The
    // trust rides the reader instance, set once at the construction site, never
    // inferred from the bytes. Owned per-instance, like View/Sign.
    private readonly string? _template;

    public Wire(global::app.View view, actor.context.@this context, bool sign = true,
        string? template = null, bool verify = true, bool deferVerify = false)
    {
        View = view;
        Sign = sign;
        _context = context ?? throw new System.ArgumentNullException(nameof(context));
        _template = template;
        _verify = verify;
        _deferVerify = deferVerify;
    }

    // Verify a signed Data on read. The OUTER transport read verifies; a nested reconstruction
    // (NestedOptions, goal-param readers) is built with verify:false — the inner Data is already
    // covered by the outer signature, so re-verifying each layer is wrong (and needs no actor).
    private readonly bool _verify;

    // Defer the async verify to the async deserialize caller instead of sync-waiting inside the
    // `ref`-struct reader. Set on the plang serializer's read wires (they verify in DeserializeAsync).
    private readonly bool _deferVerify;

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

    /// <summary>
    /// Read options carrying a context-ful Wire — the single place that builds them (a
    /// nested Data / goal.call params deserialize through the Wire, so they need it; Options.Read
    /// alone has no Wire). One source for the goal-read, nested-Data, and goal.call options.
    /// Takes the whole <see cref="ReadContext"/> — never its decomposed fields.
    /// </summary>
    internal static JsonSerializerOptions ReadOptions(global::app.type.reader.ReadContext ctx)
    {
        var options = global::app.channel.serializer.json.Options.Read(ctx.Context);
        options.Converters.Add(new Wire(ctx.View, context: ctx.Context, template: ctx.Template, verify: ctx.Verify, deferVerify: ctx.DeferVerify));
        return options;
    }

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
        // @schema dispatch: probe a struct COPY of the reader for the first property's value
        // (the writer always emits @schema first), then hand the untouched reader to that
        // schema's reader — `data` reads the envelope, `signature` verifies + peels. No
        // `if signature` special-case; the registry owns the layers.
        var probe = reader;
        probe.Read();
        string schema = @this.WireSchemaData;
        if (probe.TokenType == JsonTokenType.PropertyName && probe.GetString() == @this.WireSchema)
        {
            probe.Read();
            schema = probe.GetString() ?? @this.WireSchemaData;
        }

        _readDepth.Value++;
        try
        {
            var jr = new global::app.channel.serializer.json.Reader(reader, buffer);
            // _context is non-null (born-in-ctor); the Store read routes through the typed wire
            // reader that binds nested typed entries.
            var ctx = new global::app.type.reader.ReadContext(_context, _template, View, _verify, _deferVerify);
            var bodyData = global::app.data.schema.@this.Instance.Reader(schema).Read(ref jr, ctx);
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


    // A Data is WRITTEN via Data.Output (value-owns-serialization) through the channel/SerializeAsync —
    // signing + signature-hoisting live there now. This converter is registered for READS only
    // (Wire.ReadOptions → Wire.Read); STJ never serializes a Data through it. The override exists only
    // to satisfy JsonConverter<Data>.
    public override void Write(Utf8JsonWriter writer, @this data, JsonSerializerOptions options)
        => throw new System.NotSupportedException(
            "Wire is a read-only converter — a Data writes itself via Data.Output, not STJ.");
}
