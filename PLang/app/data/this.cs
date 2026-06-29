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
// A Data writes itself via Data.Output (value-owns-serialization) and reads through a context-ful
// Wire registered in options (Wire.ReadOptions / the channel). There is NO context-less default
// converter — every path that touches a Data carries context; born-with-context, no fail-open.
public partial class @this
{
    // THE value — the typed instance. It IS the value; Data never looks inside
    // it, never asks what type it holds, has no special cases for any type.
    // NEVER C# null: absence is a typed citizen (`absent` for NotFound/
    // Uninitialized, null.@this for present-null), so no consumer ever
    // null-checks the value slot. Everything the old shape kept beside the
    // value (raw source form, descriptor, lazy factory) lives ON the instance:
    // a file holds its own bytes, a source its declared {type, kind}, a
    // computed its factory.
    private protected global::app.type.item.@this _item = global::app.type.@null.@this.Instance;
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

    // Context RIDES THE VALUE. A value is born with context at the one
    // creation point (the I/O input layer / resolve); the Data wrapper only
    // ever inherits it, never erases it. The getter falls back to the value's
    // own context so a wrapper minted context-less (a static `Ok(pathWithCtx)`)
    // still answers the context the value carries. The setter propagates only
    // a NON-null context downward — it can adopt or rebind, never clobber a
    // value that already knows its context.
    [JsonIgnore]
    public actor.context.@this Context
    {
        get => _context ?? (_item as module.IContext)?.Context!;
        set
        {
            _context = value;
            if (value != null && _item is module.IContext contextual)
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
    /// True when the value IS a single live <c>%variable%</c> reference (a
    /// stamped template whose whole text is one ref). Build-time validation
    /// skips such values — they resolve at runtime. Stamp-based: an unstamped
    /// "%x%" is literal text, not a reference.
    /// </summary>
    [JsonIgnore]
    public bool IsVariable => _item.IsRef(out _);

    /// <summary>
    /// True when the value carries any live <c>%variable%</c> reference (the
    /// builder's template stamp). <see cref="IsVariable"/> is "%name%" (the
    /// whole value IS a reference); this is "hello %name%" too.
    /// </summary>
    [JsonIgnore]
    public bool HasVariableReference => _item?.Template != null;

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


    [JsonConstructor]
    public @this(string name, object? value = null, type? type = null, @this? parent = null,
        actor.context.@this? context = null)
    {
        Name = CleanName(name);
        _context = context ?? parent?._context!;
        // The value is born WITH this Data's context — never context-less then
        // stamped. JSON natives-out here; a plain string stays a string for the
        // type to decide.
        var parsed = new global::app.type.item.serializer.json(_context).Parse(value);
        // A declared, non-polymorphic type owns construction — ONE door. The type
        // forks internally (raw → a lazy source at its format; a built value already
        // of the type → hold; a different built type → re-type via its hook; null →
        // typed absence). A polymorphic stamp / no declared type is the natural lift,
        // the value's own truth stands.
        if (type is { IsNull: false } && !type.Polymorphic)
            _item = type.Build(parsed, _context);
        else
            _item = global::app.type.@this.Create(parsed, _context);
        Parent = parent;
        Path = BuildPath(parent, Name);
        IsInitialized = true;
        Created = System.DateTime.UtcNow;
        Updated = Created;
    }

    /// <summary>
    /// Holds an already-deserialized <paramref name="instance"/> — no type work.
    /// The "decide the type / deserialize" step (the reader) runs at the call site
    /// BEFORE this; Data just carries the result. This is the born-typed shape: a
    /// value arrives as its type, and Data is a dumb holder. (Replaces the
    /// <c>(name, value, type)</c> ctor's Lift+Judge path — see <see cref="type.Deserialize"/>.)
    /// </summary>
    public @this(string name, global::app.type.item.@this instance, @this? parent = null,
        actor.context.@this? context = null)
    {
        Name = CleanName(name);
        _item = instance ?? global::app.type.@null.@this.Instance;
        Parent = parent;
        Path = BuildPath(parent, Name);
        IsInitialized = true;
        Created = System.DateTime.UtcNow;
        Updated = Created;
        _context = context ?? parent?._context!;
    }

    /// <summary>
    /// Applies a declared type judgement to this Data's instance after
    /// construction — the build pipeline's stamping seam (the schema overrides
    /// an LLM-emitted shape; a kind hook refines an authored literal). Same
    /// rules as the entry lift's judgement fold (<see cref="type.Judge"/>).
    /// </summary>
    internal void Declare(type declared)
    {
        if (declared is not { IsNull: false } || declared.Polymorphic) return;
        // The after-the-fact stamp routes through the SAME door as the ctor: the type
        // builds from the current (already-built) value — a value already of the type
        // holds (re-kind if needed), a different built type re-types, a %ref%/variable
        // leaf is left for its own resolution. No Build/Judge context fork.
        _item = declared.Build(_item, _context);
    }

    /// <summary>
    /// THE value door — "I am going to use this value, give it to me ready."
    /// The type does everything (loads, parses, renders — it may answer as a
    /// DIFFERENT type: file → dict); Data keeps the answer only when the type
    /// itself allows (<see cref="global::app.type.item.@this.Cacheable"/> —
    /// parse kept, render never); that one assignment IS the narrow. A failure
    /// surfaces as <see cref="Error"/>, never a throw into a courier.
    /// <para><b>Await once</b> per call site — no store-and-await-twice.</para>
    /// </summary>
    public virtual async ValueTask<global::app.type.item.@this> Value()
    {
        // No catch here — a load/parse/render failure propagates LOUD (stack
        // intact, exception never lost). The seams that own a consumed error
        // channel catch narrowly: the typed ask (slot error surfaced by the
        // pre-Run guard) and navigation (MaterializeFailed to the developer).
        var answer = await _item.Value(this);
        if (!ReferenceEquals(answer, _item) && _item.Cacheable)
        {
            if (answer is module.IContext contextual) contextual.Context = _context;
            _item = answer;
        }
        return answer;
    }

    /// <summary>
    /// The typed instance — THE value. Internal: for type-internal seams (a
    /// file reading itself through the channel) and the wire writer. Couriers
    /// move the whole Data and never reach here.
    /// </summary>
    internal global::app.type.item.@this? Instance => _item;

    /// <summary>
    /// Value door with a fallback for when the resolved value is null — absent slot
    /// or present-null. Lets a handler express a runtime/computed default a static
    /// <c>[Default(...)]</c> can't (<c>await Actor.Value(Context.Actor)</c>). Sync-
    /// completing when the value is already in memory.
    /// </summary>
    public async ValueTask<global::app.type.item.@this?> Value(global::app.type.item.@this? fallback)
        => await Value() ?? fallback;

    /// <summary>
    /// Replaces the value — the write side of the door. The new value lifts to
    /// its typed instance at this seam; mutation fires <see cref="OnChange"/>.
    /// </summary>
    public virtual void SetValue(object? value)
    {
        _item = global::app.type.@this.Create(new global::app.type.item.serializer.json(_context).Parse(value), _context);
        Updated = System.DateTime.UtcNow;
        IsInitialized = true;
        if (_item is module.IContext contextual)
            contextual.Context = _context;
        // Data owns OnChange — fires whenever the wrapped value mutates.
        // Constructors set _item directly and bypass this. SetValueDirect also bypasses.
        FireOnChange(this);
    }

    /// <summary>
    /// What is in memory NOW — sync, no I/O, no parse, no resolve: THE
    /// INSTANCE, whatever it currently is (an unparsed source, a carrier, a
    /// final-form value). ToString, Equals and debug views read here; they
    /// never load. A consumer that needs raw CLR is a .NET edge → Clr.
    /// </summary>
    public virtual global::app.type.item.@this Peek() => _item ?? global::app.type.@null.@this.Instance;

    /// <summary>
    /// Construct a source-backed (lazy) Data — the value is a
    /// <see cref="global::app.type.item.source"/> holding the undecoded form
    /// under its declared <c>{type, kind}</c>; the parse runs through the
    /// instance's own door on first use. Used by the channel boundary and the
    /// wire reader; <c>set %x% = 5</c> still lifts the value directly.
    /// </summary>
    public static @this FromRaw(object raw, type type, actor.context.@this? context = null, string name = "",
        string? format = null, string? template = null)
    {
        // A `json` kind is JSON-encoded (object/dict/list/structured) — read it through
        // the plang (json) serializer. Anything else is its own raw form (csv, image
        // bytes, a biginteger's digits) — read through the value serializer. The caller
        // may force a format (the wire passes application/plang for its encoded slots).
        format ??= string.Equals(type?.Kind, "json", System.StringComparison.OrdinalIgnoreCase)
            ? "application/plang" : "text/plain";
        var d = new @this(name) { _context = context! };
        d._item = new global::app.type.item.source(raw, type?.Name ?? "", type?.Kind, format: format, template: template) { Context = context };
        return d;
    }

    /// <summary>True when this Data is source-backed (holds an undecoded form held verbatim).</summary>
    internal bool HasRaw => _item is global::app.type.item.source;

    /// <summary>The undecoded source form, or null for an authored/parsed value. Internal — never on the wire.</summary>
    internal object? Raw => (_item as global::app.type.item.source)?.Raw;

    /// <summary>
    /// True when this Data is source-backed and has NOT been parsed or mutated —
    /// the verbatim-passthrough condition. Its raw source form can serialize back
    /// out untouched, with no parse-then-reserialize. (A parse rebinds the
    /// instance away from the source, so source-typed means untouched.)
    /// </summary>
    internal bool RawUntouched => _item is global::app.type.item.source;

    /// <summary>
    /// Updates the instance without triggering Value setter side effects (no unwrap,
    /// no OnChange). Used by RehydrateNestedData and the wire/compress couriers —
    /// transitional debt the schema-layer branch deletes; do not add callers.
    /// A non-item value (a reconstructed Data riding as a courier payload) is
    /// carried by the rung-2 wrapper so the slot stays item-typed.
    /// </summary>
    /// <summary>
    /// Marks this Data's value as builder-authored — the template seam. A text
    /// containing <c>%ref%</c> holes rebinds to a stamped copy
    /// (<c>Template = "plang"</c>); a container with refs inside is rebuilt
    /// stamped (nested entries restamp in place), so use-time knows something
    /// needs rendering without walking. Idempotent; a value with no holes is
    /// untouched. Called by the authored seams only (.pr load via
    /// <c>Action.StampTemplates</c>, wire-rebuilt actions, test fixtures
    /// authoring actions directly) — runtime input never passes here.
    /// </summary>
    [System.Obsolete("Templates are stamped on read via ctx.Template (Wire.ReadOptions), not eagerly. " +
        "Do not add new callers; existing ones are legacy to migrate.")]
    internal @this Authored()
    {
        if (_item is { } instance
            && StampedForm(instance) is { } stamped
            && !ReferenceEquals(stamped, instance))
            SetValueDirect(stamped);
        return this;
    }

    // The stamped form of an authored value: the instance itself when there is
    // nothing to stamp, a stamped copy otherwise. Containers recurse.
    private static global::app.type.item.@this? StampedForm(global::app.type.item.@this instance)
    {
        switch (instance)
        {
            case global::app.type.text.@this t:
                return t.Authored();

            case global::app.type.list.@this l:
            {
                if (l.Template != null) return l;
                // Materialize the rows ONCE: a row borns a fresh Data per read (no
                // cache-back), so StampEntry mutates THESE instances and the rebuilt
                // list must keep THESE — re-reading l.Items would born fresh, unstamped
                // rows and drop the stamping. (Interim — this whole walk is slated to
                // move to the parser/creation seam, where the authored flag is known.)
                var items = l.Items;
                bool any = false;
                foreach (var entry in items) any |= StampEntry(entry);
                if (!any) return l;
                return new global::app.type.list.@this(items) { Template = "plang", Context = l.Context };
            }

            case global::app.type.dict.@this d:
            {
                if (d.Template != null) return d;
                var entries = d.Entries;   // materialize once — see the list case
                bool any = false;
                foreach (var entry in entries) any |= StampEntry(entry);
                if (!any) return d;
                var stampedDict = new global::app.type.dict.@this { Template = "plang", Context = d.Context };
                foreach (var entry in entries) stampedDict.Set(entry);
                return stampedDict;
            }

            // A text-declared source off the wire (.pr params reload as
            // source instances) — a %ref% string is text-until-resolved, so
            // the stamp collapses it to a stamped text (the "parse" of a
            // text declaration is the string itself).
            case global::app.type.item.source src
                when src.Raw is string rawStr && HasTemplateRef(rawStr)
                     && src.Mint().Name is "text" or "string":
                if (src.Template != null) return src;
                return new global::app.type.text.@this(rawStr) { Template = "plang" };

            // A text-declared string riding the carrier (wire-read declared
            // params land as labeled clr) — same collapse as the source case.
            case Clr ct
                when ct.Value is string carried && HasTemplateRef(carried)
                     && ct.Mint().Name is "text" or "string":
                if (ct.Template != null) return ct;
                return new global::app.type.text.@this(carried) { Template = "plang" };

            // A raw CLR container riding the rung-2 carrier (a C#-composed
            // dict/list literal) — refs hide inside raw strings, so scan the
            // graph once at the seam; the stamp lands on the carrier.
            case Clr c
                when c.Value is IDictionary<string, object?> or IList<object?>:
                if (c.Template != null || !RawGraphHasRef(c.Value, 0)) return c;
                return new Clr(c.Value) { Template = "plang", Context = c.Context };

            default:
                return instance;
        }
    }

    // Depth-capped scan of a raw CLR container graph for %ref% strings —
    // stamp-time only, never at use.
    private static bool RawGraphHasRef(object? v, int depth)
    {
        if (depth > 8) return false;
        switch (v)
        {
            case string s: return HasTemplateRef(s);
            case global::app.type.text.@this t: return HasTemplateRef(t.ToString());
            case IDictionary<string, object?> d:
                foreach (var kv in d) if (RawGraphHasRef(kv.Value, depth + 1)) return true;
                return false;
            case IList<object?> l:
                foreach (var e in l) if (RawGraphHasRef(e, depth + 1)) return true;
                return false;
            case @this inner: return inner._item != null && RawGraphHasRef(inner._item.Peek(), depth + 1);
            default: return false;
        }
    }

    // Restamps one container entry in place (the entry Data rebinds to the
    // stamped value). True when the entry holds (or now holds) a stamp.
    private static bool StampEntry(@this entry)
    {
        if (entry._item is not { } inner) return false;
        var stamped = StampedForm(inner);
        if (stamped != null && !ReferenceEquals(stamped, inner))
            entry.SetValueDirect(stamped);
        return (stamped ?? inner).Template != null;
    }

    private static bool HasTemplateRef(string value)
        => global::app.type.text.@this.HasHoles(value);

    internal void SetValueDirect(object? value)
    {
        // A bare Data may not become a value — wrapping one in a Clr makes the
        // carrier reflect the Data on the wire, recursively ({name,value,{name,value,…}}).
        // Same rule as type.Create: nested Data rides inside an owning wrapper type,
        // never as the value itself. The throw flags the offending caller.
        if (value is @this)
            throw new System.InvalidOperationException(
                "A bare Data may not be stored as a value (SetValueDirect) — it would ride a clr carrier "
                + "and reflect recursively on the wire. Pass the inner value, not the Data wrapper.\n"
                + System.Environment.StackTrace);
        _item = value is null ? null
            : value as global::app.type.item.@this
            ?? new Clr(value);
        // Context propagates immediately — a context-resolved identity (the
        // carrier's registry name) must be stable from the first mint, or the
        // signed canonical form drifts when a later bind stamps Context.
        if (_item is module.IContext contextual && _context != null)
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
            if (_item == null) return type.Null;
            var minted = _item.Type;
            foreach (var entry in minted.List) entry.Context ??= _context;
            return minted;
        }
    }

    /// <summary>
    /// The value's kind — read from the instance's own entity (the single
    /// owner). Stays null for types without a kind.
    /// </summary>
    [JsonIgnore]
    public string? Kind => _item?.Mint().Kind;

    /// <summary>
    /// Enumerates as (key, value) Data pairs. Data owns the knowledge of how to iterate:
    /// dictionaries yield (dictKey, dictValue), lists yield (index, element),
    /// single values yield (0, value). All results are Data — callers never see raw objects.
    /// </summary>
    public IEnumerable<(@this key, @this value)> EnumerateItems()
        => _item.EnumerateItems(_context);

    /// <summary>Emptiness — the binding answers for absence (uninitialized,
    /// no value); the INSTANCE answers for its own emptiness (text knows
    /// whitespace, dict/list know zero entries, null knows it is empty).</summary>
    public async ValueTask<bool> IsEmpty()
        => !IsInitialized || await _item.IsEmpty();

    /// <summary>Presence — the binding's own question (absence is Data's one
    /// concern): initialized and holding neither absence citizen. Distinct
    /// from emptiness ("" and false are present) and truthiness.</summary>
    public bool HasValue => IsInitialized
        && _item is not global::app.type.@null.@this;

    // The null *value* — a present null carrying the null.@this singleton, so
    // IsInitialized is true (distinct from NotFound/Uninitialized, which leave a
    // null `data` reference with IsInitialized false). The singleton hosts null's
    // behavior (always falsy, null==null) so `is null` value-switches dissolve.
    public static @this Null(string name = "") => new(name, app.type.@null.@this.Instance);
    public static @this NotFound(string name = "")
    {
        var d = new @this(name);
        d._item = global::app.type.@null.@this.Instance;
        d.IsInitialized = false;
        return d;
    }
    public static @this Uninitialized(string name) => NotFound(name);

    /// <summary>
    /// True when the held instance carries the builder-authored template stamp —
    /// the gate every %ref% resolution branch checks. Unstamped values (runtime
    /// input, stored results) never resolve; their "%...%" content is literal.
    /// </summary>
    private bool IsStampedTemplate => _item?.Template != null;

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
    /// THE typed ask — "I need a T." One branchless line: the door makes the
    /// value ready (load/parse/render — the TYPE does everything), then the
    /// TARGET constructs itself from the answer
    /// (<see cref="global::app.type.item.ICreate{TSelf}"/> — pass-through and
    /// chain facet by default, real conversions per type, decline with the
    /// type's own reason on the returned envelope). <b>Conversion never
    /// rebinds</b> — the answer is a view for this caller; only
    /// <see cref="Value()"/>'s own answer rebinds. Context rides this Data —
    /// never a parameter.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<T?> Value<T>()
        where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        // A name slot wants the reference itself (its name), not its value — so it must
        // NOT open the door (the door resolves a reference to what it points at). Every
        // other slot opens the door: a reference resolves through it, a container
        // deep-renders, and the resolved value's own type then converts to T.
        if (typeof(T) == typeof(global::app.variable.@this) && Peek() is global::app.variable.@this nameRef)
            return T.Create(nameRef, this);
        return T.Create(await Value(), this);
    }

    /// <summary>
    /// Forms the typed slot binding from this resolved Data and an already-built
    /// answer instance — a <c>Data&lt;T&gt;</c> view under THIS binding's identity
    /// (Name, Context; Properties and event lists aliased by reference). The
    /// answer is the instance the typed ask produced; a null answer carries this
    /// binding's failure across (the typed ask's decline landed it here via
    /// <c>Fail</c>), so the formed slot's <c>Success</c> mirrors the source.
    /// </summary>
    public @this<T> ShallowClone<T>(T? answer) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        var clone = new @this<T>(Name, null, null, Parent)
        {
            Returned = Returned,
            ReturnDepth = ReturnDepth,
            Warnings = Warnings != null ? new List<Info>(Warnings) : null,
            Properties = Properties,
        };
        clone._item = answer ?? global::app.type.item.@this.Absent;
        // Context rides the value — prefer the answer's own (born at I/O /
        // resolve), fall back to this binding's. Never push null down.
        clone.Context = (answer as module.IContext)?.Context ?? Context;
        clone.OnCreate = OnCreate;
        clone.OnChange = OnChange;
        clone.OnDelete = OnDelete;
        // A declined ask lands its reason on this binding (asking.Fail) and
        // answers null — carry that failure onto the typed view the caller holds.
        if (answer == null && _error != null)
        {
            clone.Fail(_error);
            clone.IsInitialized = false;
        }
        return clone;
    }

    /// <summary>
    /// The typed FACE of this binding — a <see cref="@this{T}"/> over the SAME value, with
    /// NO resolution and NO clone of the value. Shares <c>_item</c>, Context, Properties and
    /// event lists by reference: this binding and the view are two handles on one variable.
    /// It is <see cref="Value{T}"/> MINUS the resolve — the dispatch hands a typed view onto
    /// the action's property; the handler's own <c>.Value()</c> opens the door later. An
    /// already-<c>Data&lt;T&gt;</c> binding is returned as itself.
    /// </summary>
    internal @this<T> As<T>() where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        if (this is @this<T> already) return already;
        var view = new @this<T>(Name, null, null, Parent)
        {
            Returned = Returned,
            ReturnDepth = ReturnDepth,
            Warnings = Warnings,
            Properties = Properties,
            IsInitialized = IsInitialized,
        };
        view._item = _item;
        view.Context = _context;
        view.OnCreate = OnCreate;
        view.OnChange = OnChange;
        view.OnDelete = OnDelete;
        if (_error != null) view.Fail(_error);
        return view;
    }

    /// <summary>The typed view, stamped with the execution <paramref name="context"/> so
    /// the handler's later <c>.Value()</c> resolves in the right scope. The dispatch form:
    /// <c>action.Parameters["name"].As&lt;T&gt;(context)</c>.</summary>
    internal @this<T> As<T>(actor.context.@this? context) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        var view = As<T>();
        if (context != null) view.Context = context;
        return view;
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

        // Full-match live-variable hop — the canonical IS the variable's own
        // Data (mutations stay visible through Variables.Get). Stamp-gated:
        // an unstamped "%x%" is literal text and `this` is already canonical.
        if (context?.Variable != null && _item.IsRef(out var varName))
        {
            var resolved = await context.Variable.Get(varName);
            if (resolved == null || !resolved.IsInitialized)
            {
                var notFound = new @this(varName, null, null, Parent, context: context);
                notFound.IsInitialized = false;
                return notFound;
            }
            return resolved;
        }

        // Any other stamped template (partial text, container with nested
        // refs) — the door renders (the TYPE fills its own holes; never
        // cached); a transient Data carries the answer under the slot's name
        // with aliased state.
        if (_item is { Template: not null } && (context ?? _context) != null)
        {
            if (_context == null!) Context = context!;
            var rendered = await Value();
            var transient = new @this(Name, rendered, null, Parent, context: _context);
            transient.Properties = Properties;
            transient.OnCreate   = OnCreate;
            transient.OnChange   = OnChange;
            transient.OnDelete   = OnDelete;
            return transient;
        }

        // Literal value — `this` is the canonical, return as-is.
        return this;
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
        if (!IsInitialized || _item == null) return false;
        // The value owns its own truthiness (empty text / zero number / empty
        // dict / null are falsy) — the instance answers; there is no CLR case
        // table here. The carrier's truthiness covers a rung-2 POCO (present →
        // truthy), the source's its raw form.
        return _item.IsTruthy();
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
        if (!IsInitialized) return false;
        // Resolve ONCE and ask the RESOLVED value its truthiness — a stamped
        // template (`cache: %off%`) must render before answering, else the
        // unrendered text reads truthy. A value that resolves its own boolean
        // meaning with I/O (path → "does it exist") answers via the marker.
        var resolved = await Value();
        if (resolved is IBooleanResolvable resolvable) return await resolvable.AsBooleanAsync();
        return resolved?.IsTruthy() ?? false;
    }

    /// <summary>
    /// Creates a new Data wrapper around the same value (no deep copy).
    /// Use when renaming — the value stays shared so mutations propagate.
    /// Events (OnChange/OnCreate/OnDelete) are intentionally not copied —
    /// clones that go through Variables.Set() get events wired at storage time.
    /// </summary>
    public @this ShallowClone() => ShallowClone(Name);

    /// <summary>
    /// Shallow clone under a new name — same value instance, type and signature
    /// (shared by reference); the property BAG is copied while the values inside
    /// it stay shared by pointer, so `set %y!NewProp% = 1` lands on %y% only.
    /// Renaming a value into a new slot (a goal-call parameter, `set %y% = %x%`)
    /// without copying or re-serializing it, so signed/typed values survive.
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
            Properties = Properties.Clone()
        };
        // The instance is shared by reference — values are immutable, so
        // sharing is always safe; the clone is a new Data pointing at the same
        // value (the `set %y% = %x%` rule).
        clone._item = _item;
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
            Properties = Properties.Clone()
        };
        clone._item = _item.Clone();
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

    /// <summary>The .NET-edge read with a fallback: resolves the value and lowers
    /// it to the CLR <typeparamref name="TClr"/> (the type's own
    /// <see cref="@this.Clr{T}"/>), answering <paramref name="fallback"/> when the
    /// value is absent. One honest expression for "give me the primitive here,
    /// this default if there's nothing" at a real .NET boundary (a string dict
    /// key, a .NET API arg).</summary>
    public async ValueTask<TClr> Clr<TClr>(TClr fallback)
    {
        var v = await Value();
        return v is null ? fallback : v.Clr<TClr>() ?? fallback;
    }

    /// <summary>Structural conversion of THIS row to <typeparamref name="T"/> — the
    /// value converts itself (<see cref="@this.Clr{T}"/>) from its in-memory form
    /// (<see cref="Peek"/>), not its resolved one. A plang-typed target keeps its
    /// door (a <c>text</c> asked for as <c>text</c> is identity), so resolution stays
    /// deferred to the perimeter. The sync, structural sibling of
    /// <see cref="Clr{TClr}(TClr)"/> — used by a consumer enumerating a list:
    /// <c>row.Clr&lt;LlmMessage&gt;()</c>.</summary>
    internal T? Clr<T>() => Peek().Clr<T>();
}

/// <summary>
/// Generic Data that carries a strongly-typed value.
/// Inherits from Data, so it satisfies Task&lt;Data&gt; in the interface chain.
/// </summary>
// Data<T> reads/writes through the same context-ful Wire (reads) and Output (writes) as Data —
// CanConvert covers the generic; there is no type-attribute converter.
public class @this<T> : @this
    where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
{
    /// <summary>
    /// Typed value door — the <see cref="@this.Value"/> door, narrowed to
    /// <typeparamref name="T"/>: the same one-line typed ask (the TARGET
    /// constructs itself via its Create), answering the instance directly.
    /// A decline lands its typed error on THIS binding (the slot the handler
    /// observes), answer null.
    /// </summary>
    public new ValueTask<T?> Value() => Value<T>();

    public @this(string name = "", T? value = default, type? type = null, @this? parent = null,
        actor.context.@this? context = null)
        : base(name, value, type, parent, context) { }

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

