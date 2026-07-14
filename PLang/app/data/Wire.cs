using System.Text.Json;

namespace app.data;

using type = global::app.type.@this;

/// <summary>
/// Wire reader for <c>app.data.@this</c> — the single point where the canonical
/// five-field shape <c>{name, type, value, properties, signature}</c> is parsed.
/// The <c>value</c> slot is read by the value's own reader (via the schema registry);
/// no per-type converter is needed. Read-only: a Data WRITES itself via
/// <c>Data.Output</c> (signing lives there), so this type only reads — through the
/// buffer-owning <see cref="ReadBuffered"/> entry.
/// </summary>
public class Wire
{
    /// <summary>
    /// The view this reader reads in. Owned per-instance — the plang serializer keeps
    /// separate Wire readers for outbound (<see cref="global::app.View.Out"/>) and store
    /// (<see cref="global::app.View.Store"/>) paths. Treating this as data on the reader
    /// (rather than AsyncLocal ambient state) keeps the storage-vs-wire decision visible
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
    /// rehydrate. (Retained on the reader because the plang serializer's write
    /// side reads it off the same construction site.)
    /// </summary>
    public bool Sign { get; }

    // The actor context a lazily-deferred value slot carries so it can
    // materialize through the reader registry on touch. Owned per-instance, like
    // View, set at the per-actor serializer's construction site.
    private readonly actor.context.@this _context;

    // The authored-content mode threaded into every value read this reader
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
    // (goal-param readers) is built with verify:false — the inner Data is already covered by the
    // outer signature, so re-verifying each layer is wrong (and needs no actor).
    private readonly bool _verify;

    // Defer the async verify to the async deserialize caller instead of sync-waiting inside the
    // `ref`-struct reader. Set on the plang serializer's read wires (they verify in DeserializeAsync).
    private readonly bool _deferVerify;

    // Hard ceiling on nested Data depth. The schema reader recurses into a nested
    // Data through this reader, so the budget must apply cumulatively across the
    // whole read (security v1 F1: pre-auth StackOverflow DoS at ~500 levels,
    // ~11 KB payload). The AsyncLocal counter threads through every ReadCore
    // invocation so the budget bounds the C# call-stack recursion. 64 mirrors
    // STJ's default depth; throws JsonException past it so the catch in
    // plang.@this.DeserializeAsync turns it into a typed PlangDeserializeError 400
    // rather than a crash.
    private const int MaxReadDepth = 64;
    private static readonly AsyncLocal<int> _readDepth = new();

    // Entry read over OWNED bytes — a structured value slot is sliced from the buffer raw
    // (no DOM). The plang serializer's DeserializeAsync and the snapshot section read drive
    // this; the bytes ARE the buffer.
    public @this? ReadBuffered(byte[] bytes)
    {
        var reader = new Utf8JsonReader(bytes);
        reader.Read();
        return ReadCore(ref reader, bytes);
    }

    private @this? ReadCore(ref Utf8JsonReader reader, byte[]? buffer)
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
            return bodyData;
        }
        finally
        {
            _readDepth.Value--;
        }
    }
}
