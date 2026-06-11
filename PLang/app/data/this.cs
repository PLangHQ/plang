using System.Text.Json;
using System.Text.Json.Serialization;
using Force.DeepCloner;
using app.Attributes;
using app;
using app.channel.serializer;
using app.error;
using app.actor.context;
using app.Utils;

namespace app.data;

using type = global::app.type.@this;

/// <summary>
/// Wraps a variable value in App with metadata.
/// Name is the variable/parameter name, Value is the data accessed via %name%.
/// Also serves as the universal result type (replaces Return).
/// Partial class — split by concern: data.cs (core), data.Result.cs, data.Navigation.cs, data.Transport.cs.
/// </summary>
// A Data serializes as the canonical {@schema, name, type, value, …} shape on EVERY STJ
// path via WireLocal, so it round-trips back to a Data (marker recognized) instead of a
// reflected map. The channel's options-registered signing Wire outranks this on the wire.
[System.Text.Json.Serialization.JsonConverter(typeof(WireLocal))]
public partial class @this
{
    // THE value — the typed instance. It IS the value; Data never looks inside
    // it, never asks what type it holds, has no special cases for any type.
    // Null only for an absent slot (NotFound/Uninitialized) or an error-only
    // result. Everything the old shape kept beside the value (_raw source
    // form, the _type descriptor, the lazy factory) lives ON the instance now:
    // a file holds its own bytes, a source holds its declared {type, kind},
    // a computed holds its factory.
    private protected global::app.type.item.@this? _instance;
    private protected actor.context.@this _context = null!;

    /// <summary>
    /// Wire marker. Every Data written to the application/plang wire carries
    /// <c>"@schema":"data"</c> — the language-agnostic signal that a JSON object IS a
    /// Data, not a user map that happens to share key names. The read side recognizes
    /// a Data strictly by this marker, never by sniffing value/type/name shape. The
    /// <c>@</c> sigil (JSON-LD convention) marks it reserved so a user map never
    /// collides; the value <c>data</c> names this format (a future schema could name
    /// another). Not a property of Data — pure wire identity, written by the wire
    /// writers and read by the recognizers.
    /// </summary>
    public const string WireSchema = "@schema";
    /// <summary>The <see cref="WireSchema"/> value that identifies a Data.</summary>
    public const string WireSchemaData = "data";

    /// <summary>
    /// True when a JSON object carries the <c>@schema:"data"</c> marker — i.e. it IS a
    /// serialized Data, not a user map. The single recognizer for every read path (the
    /// universal json entry parse (<c>item.serializer.json.Parse</c>) and the wire reader), so a Data is
    /// lifted back to a Data everywhere a marked object is parsed.
    /// </summary>
    internal static bool IsDataMarked(System.Text.Json.JsonElement element)
        => element.ValueKind == System.Text.Json.JsonValueKind.Object
           && element.TryGetProperty(WireSchema, out var s)
           && s.ValueKind == System.Text.Json.JsonValueKind.String
           && s.GetString() == WireSchemaData;

    /// <summary>Cache for As&lt;T&gt;() Resolve method lookups — avoids per-call reflection.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Reflection.MethodInfo?>
        ResolveMethodCache = new();

    // Subscribers as Lists (not C# events) so cross-type wraps (As<T>) and clones can
    // share the same list ref between source and view. C# events are immutable
    // multicast delegates and can't be reference-shared. Direct .Add(...) from the
    // outside is fine — these are internal infrastructure, not a public-API
    // contract that needs encapsulation.

    /// <summary>Subscribers fired by Variables.Set() when this Data is replaced — (oldData, newData).</summary>
    [JsonIgnore]
    [LlmIgnore]
    public List<Action<@this, @this>> OnChange { get; set; } = new();

    /// <summary>Subscribers fired when variable is first created in the store — (data).</summary>
    [JsonIgnore]
    [LlmIgnore]
    public List<Action<@this>> OnCreate { get; set; } = new();

    /// <summary>Subscribers fired by Variables.Remove() before deletion — (data).</summary>
    [JsonIgnore]
    [LlmIgnore]
    public List<Action<@this>> OnDelete { get; set; } = new();

    /// <summary>Fires every OnChange subscriber in order.</summary>
    public void FireOnChange(@this newData)
    {
        foreach (var h in OnChange) h.Invoke(this, newData);
    }

    /// <summary>Fires every OnCreate subscriber in order.</summary>
    public void FireOnCreate()
    {
        foreach (var h in OnCreate) h.Invoke(this);
    }

    /// <summary>Fires every OnDelete subscriber in order.</summary>
    public void FireOnDelete()
    {
        foreach (var h in OnDelete) h.Invoke(this);
    }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonIgnore]
    public actor.context.@this Context
    {
        get => _context;
        set
        {
            _context = value;
            if (_instance is module.IContext contextual)
                contextual.Context = value;
        }
    }

    [JsonIgnore]
    public string Path { get; }

    [JsonIgnore]
    [LlmIgnore]
    public @this? Parent { get; }

    [JsonIgnore]
    public bool IsInitialized { get; protected set; }

    /// <summary>
    /// True when the raw _value is a %variable% reference (starts and ends with %).
    /// Used to skip build-time validation on values that resolve at runtime.
    /// </summary>
    // A %var%-reference value rides as text.@this after born-native (a .pr loads
    // "%x%" through UnwrapJsonElement). The reference lives in the string either
    // way, so resolution peeks through the instance to the backing string.
    private string? VarString => Peek() as string ?? (_instance as app.type.text.@this)?.Value;

    [JsonIgnore]
    public bool IsVariable => VarString is string s && s.StartsWith('%') && s.EndsWith('%') && s.Length > 2;

    /// <summary>
    /// True when the raw _value contains any %variable% reference anywhere.
    /// IsVariable is "%name%" (the whole value IS a variable).
    /// HasVariableReference is "%count% + 1", "hello %name%", etc. (contains one or more).
    /// </summary>
    [JsonIgnore]
    public bool HasVariableReference => VarString is string s && System.Text.RegularExpressions.Regex.IsMatch(s, @"%[^%]+%");

    [JsonIgnore]
    public DateTime Created { get; }

    [JsonIgnore]
    public DateTime Updated { get; private set; }

    // Field initializer (not constructor assignment) so identity-preserving wraps (As<T>)
    // can override via `new Data<T>(...) { Properties = source.Properties }` and have the
    // initializer win — assignments in the object initializer fire AFTER field initializers
    // AND after the constructor body.
    //
    // [Out, Store] on this and the other envelope properties (Value, Type, Error,
    // Success, Signature) is documentation, not active filtering. Wire writes the
    // canonical {name, type, value, properties, signature} envelope by hand and
    // never consults Tagged for the Data type itself — Normalize's nested-Data
    // branch (this.Normalize.cs) short-circuits before NormalizeObject runs on
    // a Data. The tags advertise the intended wire shape; the actual wire
    // emission lives in Wire.Write.
    [JsonIgnore]
    [LlmIgnore]
    [Out, Store]
    public Properties Properties { get; set; } = new();

    /// <summary>
    /// THE STORE SEAM (Stage 9) — every value is lifted to its typed wrapper on
    /// the way IN, so the slot always holds a PLang value and no consumer ever
    /// branches on "wrapper or bare CLR?". One chokepoint; every slot write
    /// routes through it.
    ///
    /// <para>No case table: the lift IS the conversion registry. The family that
    /// owns the raw CLR type (each family's own <c>static OwnedClrTypes</c>
    /// declaration, composed by <c>convert.OwnerOf</c>) constructs the value via
    /// its own <c>Convert</c> hook — a new type joins by declaring its CLR mates
    /// and hook on itself, never by editing this seam. The date arms stay a 1:1
    /// CLR map because the DECLARATIONS are 1:1 (the seam never value-sniffs —
    /// a midnight DateTime is not a <c>date</c>).</para>
    ///
    /// <para>A value no family owns stays as-is and types as <c>item</c> — the
    /// "unknown" apex, exactly what <c>object</c> is to C#. A bare Data THROWS:
    /// nested Data always rides inside an owning wrapper type (list's pattern),
    /// so a bare one is always the implicit-operator double-wrap accident.</para>
    /// </summary>
    internal static global::app.type.item.@this? Lift(object? v, actor.context.@this? context = null)
    {
        if (v is null) return null;
        if (v is global::app.type.item.@this already) return already;
        if (v is @this)
            throw new System.InvalidOperationException(
                "A bare Data may not be stored as a value — nested Data always rides inside an owning wrapper type. "
                + "This is the implicit-operator double-wrap accident: return the inner value via its own factory, never `return innerDataInstance;`."
                + System.Environment.StackTrace);

        var (family, _) = global::app.type.convert.@this.OwnerOf(v.GetType());
        if (family != null && typeof(global::app.type.item.@this).IsAssignableFrom(family))
        {
            // kind: null — the routing kind is a conversion-TARGET nuance ("text"
            // asks text.Convert for the raw string; a precision pins a numeric
            // narrowing). The seam always wants the family's WRAPPER; the wrapper
            // derives its own kind from the value (never stored — ruling 4).
            var lifted = global::app.type.convert.@this.OfStatic(family, v, kind: null, context: context);
            if (lifted is { Success: true } && lifted.Peek() is global::app.type.item.@this wrapper)
                return wrapper;
        }
        // Unowned — rung 2: a strongly-typed C# object plang holds as `item`
        // with kind naming the class. The carrier's Peek answers the real
        // instance, so generic consumers keep seeing the object itself.
        return new global::app.type.item.clr(v);
    }

    [JsonConstructor]
    public @this(string name, object? value = null, type? type = null, @this? parent = null)
    {
        Name = CleanName(name);
        _instance = Lift(global::app.type.item.serializer.json.Parse(value));
        Parent = parent;
        Path = BuildPath(parent, Name);
        IsInitialized = true;
        Created = System.DateTime.UtcNow;
        Updated = Created;
        if (parent != null)
            _context = parent._context;
        // A bare {object|item} stamp with no kind and no strict is the
        // polymorphic NON-judgement — the value's own truth stands (and a null
        // stays the null sentinel, not a typed absence).
        if (type is { IsNull: false } && !type.Polymorphic)
            _instance = _instance != null
                ? type.Judge(_instance)
                // A declared type with no value yet — a typed absence (a tool
                // parameter slot, a typed null). The declaration must survive
                // even with nothing to lift.
                : new global::app.type.item.absent(type.Name, type.Kind);
    }

    /// <summary>
    /// Applies a declared type judgement to this Data's instance after
    /// construction — the build pipeline's stamping seam (the schema overrides
    /// an LLM-emitted shape; a kind hook refines an authored literal). Same
    /// rules as the entry lift's judgement fold (<see cref="type.Judge"/>).
    /// </summary>
    internal void Declare(type declared)
    {
        if (_instance != null && declared is { IsNull: false } && !declared.Polymorphic)
            _instance = declared.Judge(_instance);
    }

    /// <summary>
    /// THE value door — "I am going to use this value, give it to me ready."
    /// Forwards to the instance's own door: the type loads and parses ITSELF
    /// and may answer as a DIFFERENT type instance (file → dict). When the
    /// instance allows (<see cref="global::app.type.item.@this.Cacheable"/> —
    /// the answer depends on nothing but the value itself), the Data rebinds to
    /// the answer; that one assignment IS the narrow. A computed/template
    /// answer is never kept — fresh at every use. A load/parse failure surfaces
    /// as <see cref="Error"/>, never a throw into a courier.
    /// <para><b>Await once</b> per call site — no store-and-await-twice.</para>
    /// </summary>
    public virtual async ValueTask<object?> Value()
    {
        if (_instance == null) return null;
        global::app.type.item.@this answer;
        try
        {
            answer = await _instance.Ready();
        }
        catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
        {
            var real = (ex as System.Reflection.TargetInvocationException)?.InnerException ?? ex;
            var entity = _instance.Mint();
            Error = new global::app.error.Error(
                $"failed to read %{Name}% as {entity.Kind ?? entity.Name}: {real.Message}",
                "MaterializeFailed", 400) { Exception = real };
            return null;
        }
        if (!ReferenceEquals(answer, _instance) && _instance.Cacheable)
        {
            if (answer is module.IContext contextual) contextual.Context = _context;
            _instance = answer;
        }
        // Transitional return shape: the in-memory form (the instance, except
        // where the instance carries a raw form — clr carrier's POCO, an
        // unparseable source's bytes). Tightens to the instance itself when
        // the consumer tail converts (slice 2).
        return answer.Open();
    }

    /// <summary>
    /// The typed instance — THE value. Internal: for type-internal seams (a
    /// file reading itself through the channel) and the wire writer. Couriers
    /// move the whole Data and never reach here.
    /// </summary>
    internal global::app.type.item.@this? Instance => _instance;

    /// <summary>
    /// Value door with a fallback for when the resolved value is null — absent slot
    /// or present-null. Lets a handler express a runtime/computed default a static
    /// <c>[Default(...)]</c> can't (<c>await Actor.Value(Context.Actor)</c>). Sync-
    /// completing when the value is already in memory.
    /// </summary>
    public async ValueTask<object?> Value(object? fallback)
        => await Value() ?? fallback;

    /// <summary>
    /// Replaces the value — the write side of the door. The new value lifts to
    /// its typed instance at this seam; mutation fires <see cref="OnChange"/>.
    /// </summary>
    public virtual void SetValue(object? value)
    {
        _instance = Lift(global::app.type.item.serializer.json.Parse(value), _context);
        Updated = System.DateTime.UtcNow;
        IsInitialized = true;
        if (_instance is module.IContext contextual)
            contextual.Context = _context;
        // Data owns OnChange — fires whenever the wrapped value mutates.
        // Constructors set _instance directly and bypass this. SetValueDirect also bypasses.
        FireOnChange(this);
    }

    /// <summary>
    /// What is in memory NOW — sync, no I/O, no parse, no resolve. Forwards to
    /// the instance's own <see cref="global::app.type.item.@this.Peek"/>: the
    /// instance itself for a final-form value, the raw source form for an
    /// unparsed one, the carried CLR object for a rung-2 value. (ToString,
    /// Equals, debug views read here; they never load.)
    /// </summary>
    public virtual object? Peek() => _instance?.Peek();

    /// <summary>
    /// Construct a source-backed (lazy) Data — the value is a
    /// <see cref="global::app.type.item.source"/> holding the undecoded form
    /// under its declared <c>{type, kind}</c>; the parse runs through the
    /// instance's own door on first use. Used by the channel boundary and the
    /// wire reader; <c>set %x% = 5</c> still lifts the value directly.
    /// </summary>
    public static @this FromRaw(object raw, type type, actor.context.@this? context = null, string name = "")
    {
        var d = new @this(name) { _context = context! };
        d._instance = new global::app.type.item.source(raw, type?.Name ?? "", type?.Kind) { Context = context };
        return d;
    }

    /// <summary>True when this Data is source-backed (holds an undecoded form held verbatim).</summary>
    internal bool HasRaw => _instance is global::app.type.item.source;

    /// <summary>The undecoded source form, or null for an authored/parsed value. Internal — never on the wire.</summary>
    internal object? Raw => (_instance as global::app.type.item.source)?.Raw;

    /// <summary>
    /// True when this Data is source-backed and has NOT been parsed or mutated —
    /// the verbatim-passthrough condition. Its raw source form can serialize back
    /// out untouched, with no parse-then-reserialize. (A parse rebinds the
    /// instance away from the source, so source-typed means untouched.)
    /// </summary>
    internal bool RawUntouched => _instance is global::app.type.item.source;

    /// <summary>
    /// Updates the instance without triggering Value setter side effects (no unwrap,
    /// no OnChange). Used by RehydrateNestedData and the wire/compress couriers —
    /// transitional debt the schema-layer branch deletes; do not add callers.
    /// A non-item value (a reconstructed Data riding as a courier payload) is
    /// carried by the rung-2 wrapper so the slot stays item-typed.
    /// </summary>
    internal void SetValueDirect(object? value)
    {
        _instance = value is null ? null
            : value as global::app.type.item.@this
            ?? new global::app.type.item.clr(value);
        // Context propagates immediately — a context-resolved identity (the
        // carrier's registry name) must be stable from the first mint, or the
        // signed canonical form drifts when a later bind stamps Context.
        if (_instance is module.IContext contextual && _context != null)
            contextual.Context = _context;
        Updated = System.DateTime.UtcNow;
        IsInitialized = true;
    }

    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Out, Store]
    public type Type
    {
        get
        {
            // Pure forward — the instance owns its identity and mints the
            // entity (chain included) itself; Data only stamps Context so
            // registry-backed reads (Is, fold properties) resolve.
            if (_instance == null) return type.Null;
            var minted = _instance.Type;
            foreach (var entry in minted.List) entry.Context ??= _context;
            return minted;
        }
    }

    /// <summary>
    /// The value's kind — read from the instance's own entity (the single
    /// owner). Stays null for types without a kind.
    /// </summary>
    [JsonIgnore]
    public string? Kind => _instance?.Mint().Kind;

    // Strings are atomic in plang, never IEnumerable<char>. Private to the
    // transitional raw-infra arms of EnumerateItems — the native dict/list
    // arms are the real iteration surface (the collection types own
    // iteration; text refuses by not implementing IEnumerable).
    private static bool IsPlangIterable(object? value) =>
        value is System.Collections.IEnumerable && value is not string;

    /// <summary>
    /// Enumerates as (key, value) Data pairs. Data owns the knowledge of how to iterate:
    /// dictionaries yield (dictKey, dictValue), lists yield (index, element),
    /// single values yield (0, value). All results are Data — callers never see raw objects.
    /// </summary>
    public IEnumerable<(@this key, @this value)> EnumerateItems()
    {
        var v = Peek();
        if (v is app.type.dict.@this nativeDict)
        {
            foreach (var entry in nativeDict.Entries)
                yield return (new @this("", entry.Name) { Context = _context }, entry);
            yield break;
        }

        if (v is app.type.list.@this nativeList)
        {
            int li = 0;
            foreach (var item in nativeList.Items)
                yield return (new @this("", li++) { Context = _context }, item);
            yield break;
        }

        // Raw infra collections (the native dict/list above handle value-flow). An
        // element already a Data passes through; a bare value is wrapped. WrapItem
        // is gone — native collections hold Data, so there's no general raw→Data path.
        if (v is IDictionary<string, object?> typedDict)
        {
            foreach (var kvp in typedDict)
                yield return (new @this("", kvp.Key) { Context = _context },
                              kvp.Value is @this kd ? kd : new @this("", kvp.Value) { Context = _context });
            yield break;
        }

        if (v is System.Collections.IDictionary untypedDict)
        {
            foreach (System.Collections.DictionaryEntry entry in untypedDict)
                yield return (new @this("", entry.Key) { Context = _context },
                              entry.Value is @this ed ? ed : new @this("", entry.Value) { Context = _context });
            yield break;
        }

        int index = 0;
        if (IsPlangIterable(v))
        {
            foreach (var item in (System.Collections.IEnumerable)v!)
                yield return (new @this("", index++) { Context = _context },
                              item is @this id ? id : new @this("", item) { Context = _context });
            yield break;
        }

        if (v != null)
            yield return (new @this("", 0) { Context = _context }, this);
    }

    [JsonIgnore]
    public bool IsEmpty => !IsInitialized || Peek() == null
        || Peek() is global::app.type.@null.@this
        || (Peek() is string s && string.IsNullOrEmpty(s))
        || (Peek() is global::app.type.text.@this t && string.IsNullOrEmpty(t.Value));

    /// <summary>
    /// Returns the raw stored value without triggering the lazy factory. Under v4,
    /// .Value is also raw (no %var% substitution) — RawValue's distinction is just
    /// "skip the factory."
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public object? RawValue => Peek();

    // The null *value* — a present null carrying the null.@this singleton, so
    // IsInitialized is true (distinct from NotFound/Uninitialized, which leave a
    // null `data` reference with IsInitialized false). The singleton hosts null's
    // behavior (always falsy, null==null) so `is null` value-switches dissolve.
    public static @this Null(string name = "") => new(name, app.type.@null.@this.Instance);
    public static @this NotFound(string name = "") => new(name, null) { IsInitialized = false };
    public static @this Uninitialized(string name) => new(name, null) { IsInitialized = false };

    private static readonly System.Text.RegularExpressions.Regex FullVarMatchRegex =
        new(@"^%([^%]+)%$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// True when <paramref name="value"/> is a string of the exact shape <c>%name%</c> (no
    /// surrounding text, no second variable). Returns the bare name in <paramref name="varName"/>.
    /// Partial-interpolation strings like <c>"hello %name%"</c> or <c>"%a% and %b%"</c> return false.
    /// Used by both As&lt;T&gt; and AsCanonical to decide whether to resolve through the live
    /// variable (full match) or treat the value as opaque text (partial / literal).
    /// </summary>
    internal static bool TryFullVarMatch(string value, out string varName)
    {
        var m = FullVarMatchRegex.Match(value);
        if (m.Success) { varName = m.Groups[1].Value; return true; }
        varName = "";
        return false;
    }

    /// <summary>
    /// THE typed ask — the untyped ask with an expectation attached: "I need a
    /// T". T is a plang type only. Mechanics: a %var% reference resolves first
    /// (the resolution preamble — substitution, the raw-name and
    /// action-destination carve-outs); then the answer is T or has T in its
    /// chain → hand over (the facet); else the answer converts itself through
    /// its own Convert hook; else the returned Data carries the error.
    /// <b>Conversion never rebinds</b> — what a caller asked for is a view for
    /// that caller, handed over and never kept; only <see cref="Value()"/>'s
    /// own answer rebinds. The slot form (a handler parameter
    /// <c>Data&lt;T&gt;</c>) and this call form are the same ask in two places.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<@this<T>> Value<T>(actor.context.@this? context = null) where T : global::app.type.item.@this
    {
        context = context ?? _context;
        // The expectation may already be satisfied — the instance is T or
        // carries T in its chain. Hand over without forcing a load/parse:
        // binding a slot is not using the content.
        if (_instance is T && this is @this<T> alreadyTyped) return alreadyTyped;
        var raw = Peek(); // the source form; never %var% substituted here
        return await AsT_Impl<T>(raw, context);
    }

    /// <summary>
    /// Materializes this Data as the requested PLang type — explicit
    /// cross-type coercion. Resolves <paramref name="typeName"/> via the
    /// context's type registry to a CLR type, then runs the materializer on
    /// the raw value. Used at call sites where the caller knows the target
    /// shape at runtime — e.g. a save action passes the format inferred from
    /// the destination extension (see todos.md "file.save cross-type coercion").
    ///
    /// <para>Returns a fresh Data wrapping the materialized value; the source's
    /// own <c>.Type</c> is not consulted, only <paramref name="typeName"/>. For
    /// the implicit case where the variable's declared type drives
    /// materialization, just read <see cref="Value"/>.</para>
    ///
    /// <para>Unknown type names surface a clear error at access — the materializer
    /// lookup fails fast rather than passing a wrong CLR shape downstream.</para>
    /// </summary>
    public @this As(string typeName, actor.context.@this? context = null)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return global::app.data.@this.FromError(new global::app.error.ServiceError(
                "Data.As(typeName) requires a non-empty type name.", "InvalidTypeName", 400));

        context = context ?? _context!;

        // `as <type>/<kind>` (e.g. `as object/json`) reads toward the named
        // encoding through the reader registry — the explicit, deterministic cast
        // that resolves a type-unknown value (replacing the removed json-string
        // content-sniff). The bare `as <type>` form keeps the CLR Convert path.
        string? kind = null;
        if (typeName.Contains('/'))
        {
            var slash = typeName.IndexOf('/');
            kind = typeName[(slash + 1)..];
            typeName = typeName[..slash];
        }
        if (kind != null)
        {
            var reader = context.App.Type.Readers.Of(typeName, kind);
            if (reader != null)
            {
                // The explicit cast re-reads the SOURCE FORM — a text wrapper
                // presents its string face to the reader.
                var raw = Peek();
                if (raw is global::app.type.text.@this sourceText) raw = sourceText.Value;
                var materialized = raw == null ? null
                    : reader(raw, kind, new global::app.type.reader.ReadContext(context));
                return new @this(Name, materialized, type.Create(typeName, kind, context: context), Parent) { Context = context };
            }
        }

        var clr = context.App.Type.Clr(typeName);
        if (clr == null)
            return global::app.data.@this.FromError(new global::app.error.ServiceError(
                $"No PLang type registered under name '{typeName}'.", "UnknownType", 400));

        var converted = context.App.Type.Convert(Peek(), clr, context).Peek();
        return new @this(Name, converted, new type(typeName), Parent) { Context = context };
    }

    /// <summary>
    /// Resolves this Data as the canonical Data — used by the generator's plain `Data` property
    /// emission to bypass As&lt;T&gt; wrapping entirely (architect/v1/plan.md §Phase 2 Rule 4).
    /// Returns:
    ///  - For full-match `%var%`: the LIVE variable Data from Variables.Get (mutations to .Value
    ///    on the returned Data are visible through Variables.Get(name)).
    ///  - For literal value (no `%`): `this` (the parameter Data) — same ref.
    ///  - For partial interpolation `"hello %x%!"`: a transient Data with the interpolated value
    ///    and `this`'s Name (slot name preserved; Properties and event lists aliased).
    ///  - For unset `%var%`: a not-initialized Data with the variable's name.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<@this> AsCanonical(actor.context.@this? context = null)
    {
        context = context ?? _context;

        // A raw-backed value is a concrete source form, not a `%var%` reference —
        // it's already canonical. Return it as-is WITHOUT reading .Value (which
        // would materialize), so a lazily-read Data stays lazy as it flows through
        // result-binding, couriers, and parameter resolution.
        if (RawUntouched) return this;

        var raw = Peek();
        // The store seam lifts source strings to `text`; resolution reads the
        // SOURCE FORM through the wrapper — a "%var%" reference is text until
        // it resolves, and this is where it resolves.
        if (raw is global::app.type.text.@this sourceText) raw = sourceText.Value;

        if (raw is string strVal && strVal.Contains('%') && context?.Variable != null)
        {
            if (TryFullVarMatch(strVal, out var varName))
            {
                var resolved = await context.Variable.Get(varName);
                if (resolved == null || !resolved.IsInitialized)
                {
                    var notFound = new @this(varName, null, null, Parent) { Context = context };
                    notFound.IsInitialized = false;
                    return notFound;
                }
                return resolved;
            }
            // Partial — interpolate into a fresh value but keep slot Name + alias state from `this`.
            var interpolated = await context.Variable.Resolve(strVal);
            var transient = new @this(Name, interpolated, null, Parent) { Context = context };
            transient.Properties = Properties;
            transient.OnCreate   = OnCreate;
            transient.OnChange   = OnChange;
            transient.OnDelete   = OnDelete;
            return transient;
        }

        // Containers with potential nested %var% — walk via the shared helper so plain
        // Data and Data<T> resolve nested vars by the same rule. Without this, handlers
        // that take plain `data.@this` (e.g. variable.set) see literal "%var%" strings
        // inside lists/dicts loaded from the .pr.
        if (context != null && IsWalkableContainer(raw))
        {
            var walked = await WalkContainerVars(raw, context);
            // A wire-shaped dict IS a serialized Data — reconstruct it (value + type as a
            // whole) rather than wrapping the dict as a Data value, which would mislabel
            // the type as `object` and lose the inner value's real type.
            @this transient = IsWireShape(walked)
                ? FromWireShape(walked, Name, context)
                : new @this(Name, walked, null, Parent) { Context = context };
            transient.Properties = Properties;
            transient.OnCreate   = OnCreate;
            transient.OnChange   = OnChange;
            transient.OnDelete   = OnDelete;
            return transient;
        }

        // Literal value — `this` is the canonical, return as-is.
        return this;
    }

    // True when raw is a list/dict shape that needs nested-var walking. Mirrors the
    // checks WalkContainerVars makes — kept separate so AsCanonical can decide whether
    // to wrap into a fresh Data without invoking the walker on non-container values.
    private static bool IsWalkableContainer(object? raw) =>
        raw is IList<object?> || raw is IDictionary<string, object?>
        || raw is app.type.dict.@this || raw is app.type.list.@this;

    // Reads a wire slot from either a native dict (Get hands back the inner Data —
    // its value's type/signature stays intact) or a raw string-keyed dict, WITHOUT
    // decomposing the whole container. Only the value/type slots are ever touched,
    // so unrelated nested values keep their native Data-keying.
    internal static object? WireSlot(object? raw, string key) => raw switch
    {
        app.type.dict.@this nd => nd.Get(key)?.Peek(),
        IDictionary<string, object?> d => d.TryGetValue(key, out var v) ? v : null,
        _ => null,
    };

    // A dict that carries the `@schema:data` marker IS a serialized Data, so binding it
    // to a Data slot must reconstruct the Data (value + type as a whole), not nest the
    // dict. Strict: only the marker counts — a user map with value/type/name keys but no
    // marker stays a plain dict.
    internal static bool IsWireShape(object? raw)
        => WireSlot(raw, WireSchema) as string == WireSchemaData;

    // Reconstruct a Data from its wire shape ({name?, value, type}). The value is set
    // as a whole under its real type; a nested wire-shaped value is itself a Data. The
    // slot's name (not the wire dict's) is used so the value's identity is its content.
    internal static @this FromWireShape(object? wire, string name, actor.context.@this? context)
    {
        object? rawValue = WireSlot(wire, "value");
        object? innerValue = IsWireShape(rawValue)
            ? FromWireShape(rawValue, "", context)
            : rawValue;
        type? typeEntity = TypeFromWire(WireSlot(wire, "type"), context);
        return new @this(name, innerValue, typeEntity) { Context = context };
    }

    // Build a type entity from its wire form — a bare name string ("text") or the
    // structured {name, kind?, strict?} object (a native dict, navigated in place).
    internal static type? TypeFromWire(object? t, actor.context.@this? context)
    {
        // `strict` rides the wire as a raw bool OR a born-native @bool.@this wrapper — unwrap
        // both, else a wrapped `true` would read as false and drop the strict flag.
        static bool AsBool(object? v) => v switch
        {
            bool b => b,
            global::app.type.@bool.@this bw => bw.Value,
            _ => v != null && bool.TryParse(v.ToString(), out var p) && p,
        };
        switch (t)
        {
            case string s when !string.IsNullOrWhiteSpace(s):
                return type.Create(s, context: context);
            case app.type.dict.@this nd when nd.Get("name")?.Peek() is { } nativeName:
                return type.Create(nativeName.ToString()!,
                    nd.Get("kind")?.Peek()?.ToString(),
                    AsBool(nd.Get("strict")?.Peek()), context);
            case IDictionary<string, object?> td when td.TryGetValue("name", out var nm) && nm != null:
                string? kind = td.TryGetValue("kind", out var k) ? k?.ToString() : null;
                bool strict = td.TryGetValue("strict", out var st) && AsBool(st);
                return type.Create(nm.ToString()!, kind, strict, context);
            default:
                return null;
        }
    }

    // Walk lists/dicts to substitute nested %var% references. Always returns a fresh
    // container for IList<object?> / IDictionary<string,object?> (WalkList/WalkDict
    // allocate). Strings are NOT handled here — full-match vs. partial-interpolation
    // semantics differ between AsCanonical (returns live var Data) and AsT_Impl (recurses
    // typed), so each owns its own string path.
    private static async System.Threading.Tasks.ValueTask<object?> WalkContainerVars(object? raw, actor.context.@this context)
    {
        if (raw is IList<object?> list) return await WalkList(list, context);
        if (raw is app.type.list.@this nativeList) return await WalkNativeList(nativeList, context);
        if (raw is app.type.dict.@this nativeDict) return await WalkNativeDict(nativeDict, context);
        if (raw is IDictionary<string, object?> dict) return await WalkDict(dict, context);
        return raw;
    }

    private protected async System.Threading.Tasks.ValueTask<@this<T>> AsT_Impl<T>(object? raw, actor.context.@this? context) where T : global::app.type.item.@this
    {
        // The store seam lifts source strings to `text`; resolution reads the
        // SOURCE FORM through the wrapper (a "%var%" reference is text until it
        // resolves here) — same downstream path .pr-served raw strings took.
        if (raw is global::app.type.text.@this sourceText)
            raw = sourceText.Value;

        // Action-destination carve-out: when T is or contains Action.@this, sub-actions
        // hold raw %var% for deferred resolution at their own dispatch time. Skip the walk
        // and convert raw straight through TypeMapping. BUT — only when raw is already a
        // typed action structure. If raw is itself a `%var%` reference (e.g. `actions=%stepResult.actions%`),
        // we still have to resolve the variable to GET the action list before the carve-out
        // applies; otherwise the literal string is handed to TypeConverter which can't
        // convert "%var%" → StepActions and the build dies with "Cannot convert String to this".
        if (IsActionDestination(typeof(T))
            && !(raw is string actStr && actStr.Contains('%') && context?.Variable != null))
            return WrapAs<T>(raw, context);

        // Raw-name carve-out: types like app.variable.Variable want the literal slot
        // string — `%x%` means "the variable named x" not "x's value". Bypass the
        // %var% substitution branch and dispatch to T.Resolve(raw, context) directly.
        // Variable.Resolve strips the % and produces { Name="x" } regardless of whether
        // x is initialized — symmetric for both `%x%` and bare `x` slot forms.
        if (raw is string rawNameStr && context != null
            && typeof(app.variable.IRawNameResolvable).IsAssignableFrom(typeof(T)))
        {
            var resolveMethod = ResolveMethodCache.GetOrAdd(typeof(T), t =>
                t.GetMethod("Resolve",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null, new[] { typeof(string), typeof(actor.context.@this) }, null));
            if (resolveMethod != null)
            {
                var resolvedObj = resolveMethod.Invoke(null, new object[] { rawNameStr, context });
                if (resolvedObj is T result)
                    return ConstructWrap<T>(result, context);
            }
        }

        // String with %var% — substitute first, BEFORE fast paths. Without this ordering,
        // T=object would always match `raw is T` and short-circuit substitution.
        // No cycle guard: stored values are values, not expressions — a resolved
        // value is never re-scanned for references, so there is no recursion to
        // guard (render is single-pass by design).
        if (raw is string strVal && strVal.Contains('%') && context?.Variable != null)
        {
            if (TryFullVarMatch(strVal, out var varName))
            {
                var resolved = await context.Variable.Get(varName);
                if (resolved == null || !resolved.IsInitialized)
                {
                    // Unset var — propagate the variable's name so handler diagnostics see it.
                    // Mark as not-initialized so callers can detect the difference between
                    // "value is null" and "var doesn't exist".
                    var notFound = new @this<T>(varName, default, null, Parent) { Context = context };
                    notFound.IsInitialized = false;
                    return notFound;
                }
                if (!resolved.Success)
                    return @this<T>.FromError(resolved.Error!);
                // The expectation may already be satisfied — binding a slot is
                // not using the content, so a file bound to a Data<file> (or
                // any value bound to a Data<item>) hands over without forcing
                // a load/parse. `write out %config%` keeps its un-narrowed
                // reference this way.
                if (resolved.Instance is T)
                    return resolved.WrapAs<T>(resolved.Instance, context);
                // Stored values are values, not expressions — the door loads content
                // (file/url) but never decodes %var% text inside a stored value (only
                // .pr-born parameter forms decode, at dispatch). Type-convert only.
                // Calling on `resolved` (the live variable) preserves identity: WrapAs
                // sees `resolved` as `this` and propagates Name + Properties + event lists.
                return resolved.AsT_Convert<T>(await resolved.Value(), context);
            }
            // Partial match — interpolate once. The result is the final value; embedded
            // %var% inside the substituted text is opaque payload (matches mainstream
            // language semantics: assignment evaluates once, stored value is opaque).
            var interpolated = await context.Variable.Resolve(strVal);
            return AsT_Convert<T>(interpolated, context);
        }

        // Containers with potential nested %var% — walk before fast paths for the same reason.
        // The walked container is a fresh object; canonical is `this` (slot Data). Shared
        // with AsCanonical via WalkContainerVars so plain Data and Data<T> resolve by one rule.
        //
        // Action-destination skip: when the slot expects Action.@this or a list of them,
        // sub-action parameters MUST keep their `%var%` references for deferred resolution
        // at the sub-action's own dispatch time. WalkContainerVars/SubstitutePrimitive
        // would resolve them eagerly against the current variable store — turning
        // `"%x%"` (the LLM's "set this to %x%") into the current value of %x%, or null
        // when %x% isn't set. The `if (value is @this) return value` guard inside
        // SubstitutePrimitive only fires for already-typed Data, not for the dict
        // representation that comes off the LLM response.
        if (context != null && IsWalkableContainer(raw) && !IsActionDestination(typeof(T)))
            return WrapAs<T>(await WalkContainerVars(raw, context), context);

        // T has static Resolve(string, Context.@this) — Path-style domain types. Done before
        // the variance/wrap path because Resolve produces a fresh T from a string, not a
        // cast of an existing value.
        var staticResolved = TryStaticResolve<T>(raw, context);
        if (staticResolved != null) return staticResolved;

        // The in-memory form doesn't satisfy T and isn't its own raw answer —
        // open the value door (the type loads/parses itself, the Data rebinds)
        // and let the evolved answer satisfy T or convert. This is the typed
        // ask's core mechanic; the earlier fast paths only skip the door when
        // the expectation was already met (binding is not use).
        if (raw is global::app.type.item.@this inMemory && inMemory is not T
            && ReferenceEquals(inMemory, _instance))
        {
            _ = await Value();
            if (_instance is T evolvedT)
                return WrapAs<T>(evolvedT, context);
            raw = Peek();
        }

        // No more substitution to do — `this` is the canonical. Apply identity-preserving
        // wrap rules (same-type fast path → variance → cross-type with conversion).
        return WrapAs<T>(raw, context);
    }

    /// <summary>
    /// Type-conversion tail of <see cref="AsT_Impl"/> — no substitution. Used after a slot's
    /// %var% has already been resolved (full-match Variables.Get or partial-match interpolation):
    /// the value is final and its string content must NOT be scanned for further %var% references.
    /// Keeps the static-Resolve(string) carve-out for Path-style domain types, then delegates to
    /// WrapAs for identity-preserving wrap + conversion.
    /// </summary>
    private @this<T> AsT_Convert<T>(object? raw, actor.context.@this? context) where T : global::app.type.item.@this
    {
        var staticResolved = TryStaticResolve<T>(raw, context);
        if (staticResolved != null) return staticResolved;
        return WrapAs<T>(raw, context);
    }

    /// <summary>
    /// Path-style domain types expose a static <c>Resolve(string, context)</c> that builds a
    /// fresh T from a string (rather than casting an existing value). Returns the wrapped
    /// result, a wrapped error if Resolve threw, or <c>null</c> when this raw isn't applicable
    /// (not a string, already a T, no context, or no Resolve method) — the caller then falls
    /// through to WrapAs. Shared by AsT_Impl and AsT_Convert so the reflection lookup +
    /// invoke + wrap-or-error rule lives in one place.
    /// </summary>
    private @this<T>? TryStaticResolve<T>(object? raw, actor.context.@this? context) where T : global::app.type.item.@this
    {
        if (raw is not string srStr || context == null || raw is T) return null;
        var resolveMethod = ResolveMethodCache.GetOrAdd(typeof(T), t =>
            t.GetMethod("Resolve",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null, new[] { typeof(string), typeof(actor.context.@this) }, null));
        if (resolveMethod == null) return null;
        var (resolvedObj, resolveError) = InvokeResolve<T>(resolveMethod, srStr, context);
        if (resolveError != null) return resolveError;
        if (resolvedObj is T result) return ConstructWrap<T>(result, context);
        return null;
    }

    /// <summary>
    /// Invokes a domain type's static <c>Resolve(string, context)</c> via reflection.
    /// <c>Resolve</c> can legitimately throw (e.g. <c>path</c> raises
    /// <c>SchemeNotRegistered</c> for an unregistered scheme like <c>s3://</c>) —
    /// reflection surfaces that as <see cref="System.Reflection.TargetInvocationException"/>.
    /// Rather than let it escape <c>As&lt;T&gt;</c>, the inner exception is shaped
    /// into a failed <c>Data&lt;T&gt;</c> so the conversion failure flows as a typed
    /// error the handler can surface.
    /// </summary>
    private static (object? value, @this<T>? error) InvokeResolve<T>(
        System.Reflection.MethodInfo resolveMethod, string raw, actor.context.@this context)
        where T : global::app.type.item.@this
    {
        try
        {
            return (resolveMethod.Invoke(null, new object[] { raw, context }), null);
        }
        catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException != null)
        {
            var inner = tie.InnerException;
            if (inner is global::app.type.path.scheme.SchemeNotRegistered snr)
                return (null, @this<T>.FromError(new global::app.error.Error(snr.Message, "SchemeNotRegistered", 400)
                {
                    FixSuggestion = $"Register a factory for scheme '{snr.Scheme}' via app.Type.Scheme.Register, or use a bare/file:// path.",
                }));
            return (null, @this<T>.FromError(new global::app.error.Error(inner.Message, "ResolveFailed", 400)));
        }
    }

    /// <summary>
    /// Identity-preserving wrap — the tail of the typed ask:
    ///   1. Same-type fast path  — `this` is already @this&lt;T&gt; holding a T → return `this`.
    ///   2. Variance fast path   — value IS a T → new @this&lt;T&gt;, instance shared by ref,
    ///                              state aliased from `this`. (T : item, so the historical
    ///                              string-vs-IEnumerable carve-outs can't arise here.)
    ///   3. Chain facet          — an evolved value still answers for what it was.
    ///   4. Cross-type with conv — the value converts, else FromError; no aliasing on failure.
    ///
    /// Caller passes the substituted/walked `value` separately because raw value is what we wrap,
    /// while `this` is the canonical Data whose Name + Properties + event lists we propagate.
    /// </summary>
    private @this<T> WrapAs<T>(object? value, actor.context.@this? context) where T : global::app.type.item.@this
    {
        // Rule 1 — same-type fast path. If `this` is already Data<T> AND its instance is T,
        // return `this`. No allocation, full identity (Name, Properties, events all native).
        // Note: for action-destination carve-out the value may have been converted; but that
        // path enters here with raw, not converted, so we still match correctly.
        if (this is @this<T> sameTyped && sameTyped._instance is T)
            return sameTyped;

        // Rule 2 — variance fast path. `value` is already a T (no conversion needed) but `this`
        // is not Data<T> (e.g. plain Data, or Data<U> for U:T). Construct a new Data<T> sharing
        // the instance by ref and alias Properties + events from `this`.
        if (value is T fast)
            return ConstructWrap<T>(fast, context);

        // The chain — an evolved value still answers for what it was. A dict
        // parsed from a file satisfies a Data<file> slot with the file facet.
        if (value is global::app.type.item.@this evolved)
            for (var p = evolved.Prior; p != null; p = p.Prior)
                if (p is T facet)
                    return ConstructWrap<T>(facet, context);

        // Null arrives here only when raw was null (or substitution produced null). Construct a
        // not-initialized Data<T> with default value, aliased state from `this`.
        if (value == null)
            return ConstructWrap<T>(default, context);

        // Thread the Data's Name (the parameter/variable name) into the
        // conversion so a bind failure can name the slot.
        var (converted, error) = AppTypes.TryConvert(value, typeof(T), context, Name);
        if (error != null)
            return @this<T>.FromError(error);
        return ConstructWrap<T>((T?)converted, context);
    }

    /// <summary>
    /// Builds a new Data&lt;T&gt; that takes `this` as the canonical: Name + Type + Parent are
    /// inherited; Properties + the three event lists are aliased by reference (shared list refs
    /// so subscribers and metadata mutations are visible through both source and view).
    /// </summary>
    private @this<T> ConstructWrap<T>(T? value, actor.context.@this? context) where T : global::app.type.item.@this
    {
        var wrapped = new @this<T>(Name, value, null, Parent) { Context = context };
        wrapped.Properties = Properties;
        wrapped.OnCreate   = OnCreate;
        wrapped.OnChange   = OnChange;
        wrapped.OnDelete   = OnDelete;
        return wrapped;
    }

    private static async System.Threading.Tasks.ValueTask<List<object?>> WalkList(IList<object?> list, actor.context.@this context)
    {
        var result = new List<object?>(list.Count);
        foreach (var item in list)
            result.Add(await SubstitutePrimitive(item, context));
        return result;
    }

    private static async System.Threading.Tasks.ValueTask<Dictionary<string, object?>> WalkDict(IDictionary<string, object?> dict, actor.context.@this context)
    {
        var result = new Dictionary<string, object?>(dict.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in dict)
            result[kvp.Key] = await SubstitutePrimitive(kvp.Value, context);
        return result;
    }

    // Walk a native dict's entry values for nested %var%, preserving dict-ness so
    // downstream navigation still hits the value type. A fresh dict is built — the
    // source is never mutated (mirrors WalkDict's fresh-container contract).
    private static async System.Threading.Tasks.ValueTask<app.type.dict.@this> WalkNativeDict(app.type.dict.@this dict, actor.context.@this context)
    {
        var result = new app.type.dict.@this { Context = context };
        foreach (var entry in dict.Entries)
            result.Set(new @this(entry.Name, await SubstitutePrimitive(entry.Peek(), context)));
        return result;
    }

    // Walk a native list's element values for nested %var%, preserving list-ness and
    // each element's name. Fresh list — the source is never mutated.
    private static async System.Threading.Tasks.ValueTask<app.type.list.@this> WalkNativeList(app.type.list.@this list, actor.context.@this context)
    {
        var result = new app.type.list.@this { Context = context };
        foreach (var item in list.Items)
            result.Add(new @this(item.Name, await SubstitutePrimitive(item.Peek(), context)));
        return result;
    }

    // Shape contract: WalkList / WalkDict / SubstitutePrimitive only match the typed-generic
    // shapes IList<object?> / IDictionary<string, object?>. A non-generic IList (ArrayList)
    // or IDictionary (Hashtable) passes through to the fall-through and is returned as-is —
    // no %var% substitution. JSON ingestion is normalized to the typed forms via
    // UnwrapJsonElement / UnwrapNewtonsoftToken upstream, so this is safe in practice.
    private static async System.Threading.Tasks.ValueTask<object?> SubstitutePrimitive(object? value, actor.context.@this context)
    {
        if (value == null) return null;

        // A %var% reference rides as text.@this after born-native — unwrap to the
        // backing string so the reference resolves (a literal text with no % falls
        // through `!s.Contains('%')` and returns its plain string, unchanged).
        if (value is app.type.text.@this txt) value = txt.Value;

        if (value is string s)
        {
            if (!s.Contains('%')) return s;
            var fullMatch = System.Text.RegularExpressions.Regex.Match(s, @"^%([^%]+)%$");
            if (fullMatch.Success)
            {
                var varName = fullMatch.Groups[1].Value;
                // Preserve the original `%var%` string when the variable is unset OR
                // its resolved value is null. Returning null silently strips the
                // reference, which destroys LLM-emitted parameter values like
                // `Name = "%x%"` during build (no user variables exist yet, so every
                // %var% read returns null and the parameter slots get nulled out).
                // String.Resolve below already does this fallback for partial matches;
                // this brings full-match parity.
                var resolved = await context.Variable.Get(varName);
                // Read through the door — substitution IS the decode, and the referenced
                // variable may be dynamic (a DynamicData factory Peek would never fire).
                var rv = resolved?.IsInitialized == true ? await resolved.Value() : null;
                return rv ?? (object?)s;
            }
            return await context.Variable.Resolve(s);
        }

        if (value is IList<object?> innerList) return await WalkList(innerList, context);
        if (value is app.type.list.@this innerNativeList) return await WalkNativeList(innerNativeList, context);
        if (value is app.type.dict.@this innerNativeDict) return await WalkNativeDict(innerNativeDict, context);
        if (value is IDictionary<string, object?> innerDict) return await WalkDict(innerDict, context);

        // Non-recursion guards: don't walk into Data, Action templates, or typed Action lists.
        // Action templates retain raw %var% for deferred resolution at their own dispatch.
        if (value is @this) return value;
        if (value is global::app.goal.steps.step.actions.action.@this) return value;
        if (value is global::System.Collections.Generic.IEnumerable<global::app.goal.steps.step.actions.action.@this>) return value;

        return value;
    }

    private static bool IsActionDestination(System.Type t)
    {
        var actionType = typeof(global::app.goal.steps.step.actions.action.@this);
        if (t == actionType) return true;
        return typeof(global::System.Collections.Generic.IEnumerable<global::app.goal.steps.step.actions.action.@this>).IsAssignableFrom(t);
    }

    /// <summary>
    /// THE comparison entry — compares this value to <paramref name="other"/> and
    /// returns the sign-free <see cref="Comparison"/>. The driving type is decided
    /// from the TYPES alone (<see cref="type.Rank"/> — ranking never forces a read),
    /// then both values are awaited through the door (the only async hops) and the
    /// driver's sync <c>Compare(a, b)</c> runs in caller order — <c>Less</c> means
    /// <c>this &lt; other</c>, no sign flip.
    ///
    /// <para>Null policy lives here, above every driver: anything vs null is
    /// equality-comparable (<c>Equal</c>/<c>NotEqual</c>, never
    /// <c>Incomparable</c>) so <c>%x% == null</c> works for every type.</para>
    /// </summary>
    public async ValueTask<Comparison> Compare(@this other)
    {
        var a = await Value();
        var b = await other.Value();
        return CompareValues(other, a, b);
    }

    /// <summary>
    /// The sync comparison core — rank, null policy, driver dispatch — over values
    /// already in memory. <see cref="Compare"/> is the public door-awaiting entry;
    /// sort's phase-2 comparator (sync by construction, no await inside
    /// <c>List.Sort</c>) calls this directly on phase-1-materialised keys.
    /// </summary>
    internal Comparison CompareValues(@this other, object? a, object? b)
    {
        var driver = Type.Rank(other);
        driver.Context ??= _context ?? other._context;

        // A present null rides as the null.@this singleton — coalesce for the policy.
        if (a is app.type.@null.@this) a = null;
        if (b is app.type.@null.@this) b = null;
        if (a == null || b == null)
            return a == null && b == null ? Comparison.Equal : Comparison.NotEqual;

        return driver.Compare(a, b);
    }

    /// <summary>
    /// Creates a deep clone of this Data. Value is deep-cloned, metadata is preserved.
    /// The natural boolean meaning of this Data.
    /// Follows common language conventions: null, false, 0, "" are falsy. Everything else is truthy.
    /// </summary>
    public virtual bool ToBoolean()
    {
        if (!IsInitialized || _instance == null) return false;
        // The value owns its own truthiness (empty text / zero number / empty
        // dict / null are falsy) — the instance answers; there is no CLR case
        // table here. The carrier's truthiness covers a rung-2 POCO (present →
        // truthy), the source's its raw form.
        return _instance.IsTruthy();
    }

    /// <summary>
    /// Boolean meaning of this Data, async — when the wrapped value knows how to
    /// answer for itself (<see cref="IBooleanResolvable"/>) the question is
    /// delegated to it; otherwise it falls through to the sync <see cref="ToBoolean"/>.
    /// The canonical resolvable is <c>path</c>: <c>path.AsBooleanAsync()</c> means
    /// "does this resource exist", which the http scheme answers with I/O — hence
    /// the async signature, and hence the condition pipeline is async.
    /// 
    /// </summary>
    public virtual async System.Threading.Tasks.Task<bool> ToBooleanAsync()
    {
        if (IsInitialized && await Value() is IBooleanResolvable resolvable)
            return await resolvable.AsBooleanAsync();
        return ToBoolean();
    }

    /// <summary>
    /// Creates a new Data wrapper around the same value (no deep copy).
    /// Use when renaming — the value stays shared so mutations propagate.
    /// Events (OnChange/OnCreate/OnDelete) are intentionally not copied —
    /// clones that go through Variables.Set() get events wired at storage time.
    /// </summary>
    public @this ShallowClone() => ShallowClone(Name);

    /// <summary>
    /// Shallow clone under a new name — same value, type, signature and properties
    /// (all shared by reference). Renaming a value into a new slot (e.g. a goal-call
    /// parameter) without copying or re-serializing it, so signed/typed values survive.
    /// </summary>
    public @this ShallowClone(string newName)
    {
        var clone = new @this(newName)
        {
            Error = Error,
            Handled = Handled,
            Returned = Returned,
            ReturnDepth = ReturnDepth,
            Warnings = Warnings != null ? new List<Info>(Warnings) : null,
            Signature = Signature,
            Properties = Properties
        };
        // The instance is shared by reference — values are immutable, so
        // sharing is always safe; the clone is a new Data pointing at the same
        // value (the `set %y% = %x%` rule).
        clone._instance = _instance;
        clone.Context = _context;
        return clone;
    }

    /// <summary>
    /// Deep-clones this Data including its value. Events are intentionally not copied —
    /// clones that go through Variables.Set() get events wired at storage time.
    /// </summary>
    public virtual @this Clone()
    {
        var clone = new @this(Name)
        {
            Error = Error,
            Handled = Handled,
            Returned = Returned,
            ReturnDepth = ReturnDepth,
            Warnings = Warnings != null ? new List<Info>(Warnings) : null,
            Signature = Signature,
            Properties = Properties.Clone()
        };
        clone._instance = _instance.DeepClone();
        clone.Context = _context;
        return clone;
    }

    public override string ToString() =>
        Success ? Peek()?.ToString() ?? "(null)" : $"Error: {Error?.Message}";

    private static string CleanName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        return name.Trim().TrimStart('%').TrimEnd('%');
    }

    private static string BuildPath(@this? parent, string name)
    {
        if (parent == null)
            return name;

        if (int.TryParse(name, out _))
            return $"{parent.Path}[{name}]";

        return $"{parent.Path}.{name}";
    }
}

/// <summary>
/// Generic Data that carries a strongly-typed value.
/// Inherits from Data, so it satisfies Task&lt;Data&gt; in the interface chain.
/// </summary>
// Type attributes don't inherit through the generic, so Data<T> carries WireLocal too
// (its CanConvert/Read handle the typed wrap) — same one-shape-everywhere guarantee.
[System.Text.Json.Serialization.JsonConverter(typeof(WireLocal))]
public class @this<T> : @this
    where T : global::app.type.item.@this
{
    /// <summary>
    /// Typed value door — the <see cref="@this.Value"/> door, narrowed to <typeparamref name="T"/>.
    /// Hides the base method so <c>await dataT.Value()</c> yields <c>T?</c> directly.
    /// </summary>
    public new async ValueTask<T?> Value()
    {
        var v = await base.Value();
        if (v is T typed) return typed;
        // The chain — an evolved value still answers for the slot's type
        // (a Data<file> slot whose value parsed to dict hands the file facet).
        if (v is global::app.type.item.@this evolved)
            for (var p = evolved.Prior; p != null; p = p.Prior)
                if (p is T facet)
                    return facet;
        // Last resort: the slot's expectation converts through the one
        // converter (a view for this caller — never kept, never rebinds).
        return v == null ? default : AppTypes.ConvertTo(v, typeof(T)) as T;
    }

    public @this(string name = "", T? value = default, type? type = null, @this? parent = null)
        : base(name, value, type, parent) { }

    public static @this<T> Ok(T value, type? type = null) => new("", value, type);
    public new static @this<T> FromError(IError error) => new() { Error = error };

    /// <summary>Typed absent slot — non-null Data, <c>IsInitialized == false</c>. The
    /// optional-param null model: the reference is never null, only the value is.</summary>
    public new static @this<T> Uninitialized(string name) => new(name) { IsInitialized = false };

    /// <summary>Typed <see cref="@this.Value(object?)"/> — resolved value, or
    /// <paramref name="fallback"/> when null (absent or present-null).</summary>
    public async ValueTask<T?> Value(T fallback)
    {
        var v = await Value();
        return v ?? fallback;
    }

    /// <summary>
    /// Explicit pass-through: retype a base <see cref="@this"/> as
    /// <see cref="@this{T}"/> without re-wrapping. Use this when a method
    /// declared <c>Task&lt;Data&lt;T&gt;&gt;</c> needs to forward a base
    /// <see cref="@this"/> coming back from a provider/dispatch — bypasses the
    /// implicit-operator double-wrap (`Data&lt;T&gt;{ Value = innerData }`)
    /// because the compiler prefers this explicit factory.
    ///
    /// Intended for error/sentinel propagation across typed boundaries — the
    /// idiomatic call site is <c>if (!source.Success) return Data&lt;T&gt;.From(source);</c>.
    ///
    /// What is forwarded: Type, Error, Handled, Returned, ReturnDepth, Warnings,
    /// Signature, Snapshot, and Properties (shared reference — forwarded
    /// metadata, not deep-cloned; mutating the new Data's Properties mutates
    /// the source's).
    ///
    /// Value handling is lossy by design: <c>source.Value is T t ? t : default</c>.
    /// When the source carries a successful value not assignable to T, Value
    /// silently coerces to <c>default(T?)</c>. Safe at the idiomatic call site
    /// because that branch only fires on an errored source (Value typically
    /// null already). For the round-trip case where T = object, every value is
    /// an object and Value is always preserved. If the source is already
    /// <see cref="@this{T}"/>, it is returned unchanged.
    /// </summary>
    public static @this<T> From(@this source)
    {
        if (source is @this<T> already) return already;
        var copy = new @this<T>(source.Name, source.Peek() is T t ? t : default, source.Type)
        {
            Error = source.Error,
        };
        // Sentinel/courier passthrough (an errored or suspended source whose
        // value isn't T — the bubbled Ask): the instance rides whole so the
        // sentinel's own type and exit semantics survive the typed boundary.
        if (copy.Instance == null && source.Instance != null)
            copy.SetValueDirect(source.Instance);
        copy.Handled = source.Handled;
        copy.Returned = source.Returned;
        copy.ReturnDepth = source.ReturnDepth;
        copy.Warnings = source.Warnings != null ? new List<Info>(source.Warnings) : null;
        copy.Signature = source.Signature;
        copy.Properties = source.Properties;
        copy.Snapshot = source.Snapshot;
        return copy;
    }

    /// <summary>
    /// Allows direct assignment of T values to data.@this&lt;T&gt; properties.
    /// FOOTGUN: when T = object and the source is itself a Data subtype, this
    /// silently wraps (Data&lt;object&gt;{ Value = Data&lt;bool&gt;{...} }) instead of
    /// passing through. Code that intentionally returns a Data&lt;T&gt; from a
    /// method declared Task&lt;Data&lt;object&gt;&gt; must use the explicit factory
    /// (`data.@this&lt;object&gt;.Ok(...)`) — never `return innerData;`.
    /// </summary>
    public static implicit operator @this<T>(T value) => new("", value);
}

/// <summary>
/// Dynamic Data — a cell whose value computes fresh on every access (system
/// variables like <c>%!Now%</c>). The lazy mechanism is the TYPE's, not Data's:
/// the cell holds a <see cref="global::app.type.item.computed"/> instance whose
/// own door answers fresh and is never kept.
/// </summary>
public class DynamicData : @this
{
    public DynamicData(string name, Func<object?> valueFactory, type? type = null)
        // The declared type rides on the computed instance itself (its label),
        // not through the entry judgement — a computed answers fresh and must
        // stay reachable as the instance.
        : base(name, new global::app.type.item.computed(valueFactory, type?.IsNull == false ? type.Name : null, type?.Kind))
    {
    }
}

