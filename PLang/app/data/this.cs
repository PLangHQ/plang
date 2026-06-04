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
public partial class @this
{
    private object? _value;
    private Func<object?>? _valueFactory;

    // Lazy (Way-3) backing: the undecoded source form — `string` for a text
    // source, `byte[]` for a binary one. When set with `_value` null, `.Value`
    // materializes through the reader registry on first touch and caches into
    // `_value`; `_raw` SURVIVES materialization (verbatim passthrough +
    // signature verification ride on it) and is cleared only by a mutation.
    // Private + never on the wire — the wire shape is Data's own four fields.
    private object? _raw;
    // Materialization is a read-through; a failed read caches its error here and
    // surfaces it on the Data (best-effort `.Value` returns null). Touch-time,
    // not read-time — the point of laziness.
    private int _materializeCount;
    private type? _type;
    private actor.context.@this _context = null!;

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
            if (_type != null) _type.Context = value;
            if (_value is module.IContext contextual)
                contextual.Context = value;
        }
    }

    [JsonIgnore]
    public string Path { get; }

    [JsonIgnore]
    [LlmIgnore]
    public @this? Parent { get; }

    [JsonIgnore]
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// True when the raw _value is a %variable% reference (starts and ends with %).
    /// Used to skip build-time validation on values that resolve at runtime.
    /// </summary>
    [JsonIgnore]
    public bool IsVariable => _value is string s && s.StartsWith('%') && s.EndsWith('%') && s.Length > 2;

    /// <summary>
    /// True when the raw _value contains any %variable% reference anywhere.
    /// IsVariable is "%name%" (the whole value IS a variable).
    /// HasVariableReference is "%count% + 1", "hello %name%", etc. (contains one or more).
    /// </summary>
    [JsonIgnore]
    public bool HasVariableReference => _value is string s && System.Text.RegularExpressions.Regex.IsMatch(s, @"%[^%]+%");

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
    public @this(string name, object? value = null, type? type = null, @this? parent = null)
    {
        Name = CleanName(name);
        _value = UnwrapJsonElement(value);
        _type = type;
        Parent = parent;
        Path = BuildPath(parent, Name);
        IsInitialized = true;
        Created = System.DateTime.UtcNow;
        Updated = Created;
        if (parent != null)
            _context = parent._context;
    }

    [JsonPropertyName("value")]
    [Out, Store]
    public virtual object? Value
    {
        get
        {
            // v4 contract: .Value is the RAW stored value. No %var% substitution, no caching.
            // Resolution is the read transformation in As<T>(context); .Value preserves the
            // input form so each As<T> call resolves freshly against the current variable store.
            // Factory still resolves once on first access — that's "lazy compute," distinct from
            // variable resolution (used for DynamicData and similar lazy-init patterns).
            if (_valueFactory != null)
            {
                _value = _valueFactory();
                _valueFactory = null;
            }
            // Lazy materialize from the raw source form — only when nothing is
            // cached yet AND a raw is set. Inline-authored values (`set %x% = 5`)
            // populate `_value` and leave `_raw` null, so they never hit this
            // path and the `%var%`-resolves-fresh-per-read contract is untouched.
            if (_value == null && _raw != null)
                _value = Materialize();
            return _value;
        }
        set
        {
            _value = UnwrapJsonElement(value);
            _valueFactory = null;
            _raw = null;            // mutation — raw is no longer authoritative
            Updated = System.DateTime.UtcNow;
            IsInitialized = true;
            _type = null;
            if (_value is module.IContext contextual)
                contextual.Context = _context;
            // Data owns OnChange — fires whenever the wrapped value mutates.
            // Constructors set _value directly and bypass this. SetValueDirect also bypasses.
            FireOnChange(this);
        }
    }

    /// <summary>
    /// Scalar / output read — the access-driven rule for <c>%x%</c> and
    /// <c>write out %x%</c>. Returns the value's raw source form WITHOUT a
    /// structured parse: a text raw is the string; a byte raw decodes utf-8 when
    /// the bytes are valid utf-8, otherwise stays <c>byte[]</c> (silently
    /// mojibake-ing binary is worse than handing back bytes). An authored value
    /// (or one already materialized) returns as-is. Scalar access never
    /// materializes a structured type — only navigation (<c>%x.field%</c>) and
    /// <c>As&lt;T&gt;</c> / <c>as &lt;type&gt;</c> do.
    /// </summary>
    public object? ScalarValue
    {
        get
        {
            if (_valueFactory != null) { _value = _valueFactory(); _valueFactory = null; }
            if (_value != null) return _value;
            if (_raw is string s) return s;
            if (_raw is byte[] b) return DecodeUtf8OrBytes(b);
            return _value;
        }
    }

    // Strict utf-8 decode: returns the string when the bytes are valid utf-8,
    // else hands back the bytes unchanged (no lossy substitution).
    private static object DecodeUtf8OrBytes(byte[] bytes)
    {
        try { return new System.Text.UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes); }
        catch (System.Text.DecoderFallbackException) { return bytes; }
    }

    /// <summary>
    /// Sets a lazy value factory. Invoked on first Value access, then cached.
    /// </summary>
    public void SetValue(Func<object?> factory)
    {
        _valueFactory = factory;
        _value = null;
        IsInitialized = true;
    }

    /// <summary>
    /// Construct a raw-backed (lazy) Data — the value materializes through the
    /// reader registry on first touch of <see cref="Value"/>. The source form
    /// (<paramref name="raw"/>) is held verbatim; <paramref name="type"/> carries
    /// the Name + Kind the reader dispatches on. Used by the channel boundary
    /// (Stage 4) and tests; <c>set %x% = 5</c> still populates the value directly.
    /// </summary>
    public static @this FromRaw(object raw, type type, actor.context.@this? context = null, string name = "")
    {
        var d = new @this(name) { _context = context };
        d._raw = raw;
        d._type = type;
        if (type != null) type.Context = context;
        return d;
    }

    /// <summary>How many times this Data materialized from <c>_raw</c> — a debug probe (0 for authored values).</summary>
    internal int MaterializeCount => _materializeCount;

    /// <summary>True when this Data is raw-backed (has an undecoded source form held verbatim).</summary>
    internal bool HasRaw => _raw != null;

    /// <summary>The undecoded source form, or null for an authored value. Internal — never on the wire.</summary>
    internal object? Raw => _raw;

    /// <summary>
    /// True when this Data is raw-backed and has NOT been materialized or mutated —
    /// the verbatim-passthrough condition. Its raw source form can serialize back
    /// out untouched, with no parse-then-reserialize.
    /// </summary>
    internal bool RawUntouched => _raw != null && _value == null && _valueFactory == null;

    /// <summary>
    /// Read-through materialization: turn <c>_raw</c> into the value via the
    /// reader registry for <c>(Type.Name, Type.Kind)</c>, falling back to the
    /// type's own <c>Convert</c> for a string raw (subsumes the old
    /// <c>ConvertValue</c>). Never clears <c>_raw</c>. A failure caches an Error
    /// that names the source and returns null — touch-time, not a throw into a courier.
    /// </summary>
    private object? Materialize()
    {
        _materializeCount++;
        var t = _type;
        try
        {
            var read = _context?.App.Type.Readers.Of(t?.Name ?? "", t?.Kind);
            if (read != null)
                return read(_raw!, t?.Kind, new global::app.type.reader.ReadContext(_context));
            // Fallback — a string raw with a known type reads via the type's own
            // Convert (json→dict, WireReader, primitive coercion). Subsumes ConvertValue.
            if (_raw is string s && t != null)
                return t.Convert(s);
            return _raw; // no type to read toward — hand back the raw form (Stage 5 refines)
        }
        catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
        {
            // The reader dispatches through reflection — unwrap so the touch-time
            // error names the real cause, not a generic invocation wrapper.
            var real = (ex as System.Reflection.TargetInvocationException)?.InnerException ?? ex;
            Error = new global::app.error.Error(
                $"failed to read %{Name}% as {t?.Kind ?? t?.Name ?? "value"}: {real.Message}",
                "MaterializeFailed", 400) { Exception = real };
            return null;
        }
    }

    /// <summary>
    /// Force materialization of a string raw in place — the navigation seam that
    /// replaces the old <c>ConvertValue</c>. A typed, still-textual value is
    /// promoted to <c>_raw</c> and read through the registry; an already-raw-backed
    /// value just materializes. No-op otherwise.
    /// </summary>
    internal void ForceMaterialize()
    {
        if (_value is string raw && _type != null)
        {
            _raw = raw;
            _value = null;
        }
        if (_value == null && _raw != null)
            _ = Value; // triggers Materialize(), caches into _value, keeps _raw
    }

    /// <summary>
    /// Updates _value without triggering Value setter side effects (no type clearing, no unwrap).
    /// Used by RehydrateNestedData to replace a dictionary with a reconstructed Data object
    /// without losing the outer Type. A mutation — invalidates <c>_raw</c>.
    /// </summary>
    private void SetValueDirect(object? value)
    {
        _value = value;
        _raw = null;
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
            if (_type != null) return _type;
            // No value, no explicit type → the "null" sentinel type entity.
            // Replaces the historical `return null;`; the Wire converter
            // already skips emission for Null so the on-wire shape is
            // unchanged.  Consumers no longer carry a `Type?` null guard.
            if (_value == null) return type.Null;
            var clr = _value.GetType();
            var typeName = _context?.App.Type.Name(clr)
                           ?? AppTypes.GetPrimitiveName(clr)
                           ?? clr.Name.ToLowerInvariant();
            // Stamp ClrType directly — the value's own CLR type is the truth.
            // The name may collapse multiple CLR types (numerics → "number"),
            // so the entity can't re-derive the precise CLR mate from the name
            // alone. Build hook stamps Kind too: for "number", carry the
            // precision (int/long/decimal/…) so consumers see the same shape
            // as a build-stamped variable.
            var derived = new type(typeName, clr);
            if (typeName == "number")
            {
                // Way 3: the kind is the value's exact CLR type — the full scalar
                // tower, no float→double collapse. number owns the clr→kind map.
                derived.Kind = global::app.type.number.@this.KindNameForClr(clr);
            }
            derived.Context = _context;
            _type = derived;
            return _type;
        }
        set
        {
            // Assigning the Null sentinel means "clear my explicit type" — the
            // getter falls back to deriving from _value's CLR type.  This lets
            // call sites copy a source's Type unconditionally without checking
            // for the no-info sentinel.
            _type = value.IsNull ? null : value;
            if (!value.IsNull) value.Context = _context;
        }
    }

    /// <summary>
    /// Build-time subtype refinement — folds through to <see cref="type.Kind"/>
    /// (the entity is the single owner; no stored field on Data). Stays null
    /// for types without a kind (plain string, polymorphic results). Setting on
    /// the <see cref="@this.Null"/> sentinel is a no-op — the sentinel is shared
    /// state; a Data minted with no name + no value has no slot to refine.
    ///
    /// <para>Not serialized: kind rides the wire inside the <c>type</c> entity
    /// (<c>{name, kind?, strict?}</c>). A flat <c>kind</c> sibling would write
    /// the same value twice (OBP smell #6) — two views that can drift — and
    /// <c>Wire.ReadBody</c> has no case to read it back.</para>
    /// </summary>
    [JsonIgnore]
    public string? Kind
    {
        get => _type?.Kind;
        set
        {
            // Read through Type so the lazy derivation runs if needed. Null
            // sentinel is shared singleton — refuse to mutate.
            var t = Type;
            if (t.IsNull) return;
            t.Kind = value;
        }
    }

    /// <summary>
    /// Gets the value cast to the specified type.
    /// </summary>
    public T? GetValue<T>()
    {
        if (_value is T typed)
            return typed;

        var converted = AppTypes.ConvertTo(_value, typeof(T));
        if (converted is T result)
            return result;

        return default;
    }

    /// <summary>
    /// Gets the value converted to the specified type.
    /// </summary>
    public object? GetValue(System.Type targetType)
    {
        if (_value == null)
            return null;

        if (targetType.IsAssignableFrom(_value.GetType()))
            return _value;

        return AppTypes.ConvertTo(_value, targetType);
    }

    /// <summary>
    /// Plang treats strings as atomic, not as IEnumerable&lt;char&gt;. The single source of
    /// truth for "is this iterable per plang semantics" — used by AsEnumerable,
    /// EnumerateItems, and the As&lt;T&gt; variance-fast-path check.
    /// </summary>
    internal static bool IsPlangIterable(object? value) =>
        value is System.Collections.IEnumerable && value is not string;

    /// <summary>
    /// Plang-specific assignability for the As&lt;T&gt; variance fast path. Same as C# IsAssignableFrom
    /// EXCEPT: a string source is NOT assignable to an IEnumerable target — strings are atomic
    /// in plang. This carve-out keeps `foreach %s%` from char-iterating when %s% is a string.
    /// </summary>
    internal static bool IsPlangAssignable(System.Type target, System.Type source)
    {
        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(target) && source == typeof(string))
            return false;
        return target.IsAssignableFrom(source);
    }

    /// <summary>
    /// Enumerates the inner value. If Value is enumerable, delegates to it.
    /// If Value is a single non-enumerable item, yields it as a one-element sequence.
    /// </summary>
    public System.Collections.IEnumerable AsEnumerable()
    {
        if (IsPlangIterable(_value))
            return (System.Collections.IEnumerable)_value!;

        // Single value — treat as a list of one
        if (_value != null)
            return new[] { _value };

        return Array.Empty<object>();
    }

    /// <summary>
    /// Enumerates as (key, value) Data pairs. Data owns the knowledge of how to iterate:
    /// dictionaries yield (dictKey, dictValue), lists yield (index, element),
    /// single values yield (0, value). All results are Data — callers never see raw objects.
    /// </summary>
    public IEnumerable<(@this key, @this value)> EnumerateItems()
    {
        if (_value is app.type.dict.@this nativeDict)
        {
            foreach (var entry in nativeDict.Entries)
                yield return (new @this("", entry.Name) { Context = _context }, entry);
            yield break;
        }

        if (_value is IDictionary<string, object?> typedDict)
        {
            foreach (var kvp in typedDict)
                yield return (new @this("", kvp.Key) { Context = _context },
                              WrapItem(kvp.Value));
            yield break;
        }

        if (_value is System.Collections.IDictionary untypedDict)
        {
            foreach (System.Collections.DictionaryEntry entry in untypedDict)
                yield return (new @this("", entry.Key) { Context = _context },
                              WrapItem(entry.Value));
            yield break;
        }

        int index = 0;
        if (IsPlangIterable(_value))
        {
            foreach (var item in (System.Collections.IEnumerable)_value!)
                yield return (new @this("", index++) { Context = _context },
                              WrapItem(item));
            yield break;
        }

        if (_value != null)
            yield return (new @this("", 0) { Context = _context }, this);
    }

    // Collection-element contract: an element is EITHER a Data (e.g. a value added by
    // list.add, which carries Type/Signature) OR a bare value (e.g. an element of a list
    // parsed from JSON). Reads normalize to Data — an existing Data passes through unchanged
    // (identity, Type, Signature preserved), a bare value is wrapped. The navigator's element
    // accessor (variable/navigator/List.Element) applies the same recognition rule (it wraps
    // with the index as the name + parent link, so it can't simply call this).
    private @this WrapItem(object? item) =>
        item is @this data ? data : new @this("", item) { Context = _context };

    [JsonIgnore]
    public bool IsEmpty => !IsInitialized || _value == null ||
        (_value is string s && string.IsNullOrEmpty(s));

    /// <summary>
    /// Returns the raw stored value without triggering the lazy factory. Under v4,
    /// .Value is also raw (no %var% substitution) — RawValue's distinction is just
    /// "skip the factory."
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public object? RawValue => _value;

    public static @this Null(string name = "") => new(name, null);
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
    /// Reads this Data as Data&lt;T&gt; — the v4 resolution entry point.
    /// Walks _value, substitutes %variable% references via context.Variable,
    /// converts to T via TypeMapping, returns a fresh Data&lt;T&gt;.
    /// Every call resolves freshly against the current context — there is nothing
    /// to cache and nothing to invalidate. Caching, if any, lives on the caller.
    ///
    /// <para>
    /// Internal: this is the source-generator's resolution entry point — T is
    /// the declared C# property type, known at emit time. Public surface for
    /// type-driven materialization is <see cref="As(string)"/> (caller names
    /// the target PLang type at runtime, no generic at the call site).
    /// </para>
    /// </summary>
    internal @this<T> As<T>(actor.context.@this? context = null)
    {
        context = context ?? _context;
        var raw = Value; // factory-resolved if any; never %var% substituted
        return AsT_Impl<T>(raw, context);
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
                var raw = ScalarValue;
                var materialized = raw == null ? null
                    : reader(raw, kind, new global::app.type.reader.ReadContext(context));
                return new @this(Name, materialized, type.Create(typeName, kind, context: context), Parent) { Context = context };
            }
        }

        var clr = context.App.Type.Clr(typeName);
        if (clr == null)
            return global::app.data.@this.FromError(new global::app.error.ServiceError(
                $"No PLang type registered under name '{typeName}'.", "UnknownType", 400));

        var converted = context.App.Type.Convert(Value, clr, context).Value;
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
    public @this AsCanonical(actor.context.@this? context = null)
    {
        context = context ?? _context;

        // A raw-backed value is a concrete source form, not a `%var%` reference —
        // it's already canonical. Return it as-is WITHOUT reading .Value (which
        // would materialize), so a lazily-read Data stays lazy as it flows through
        // result-binding, couriers, and parameter resolution.
        if (RawUntouched) return this;

        var raw = Value;

        if (raw is string strVal && strVal.Contains('%') && context?.Variable != null)
        {
            if (TryFullVarMatch(strVal, out var varName))
            {
                var resolved = context.Variable.Get(varName);
                if (resolved == null || !resolved.IsInitialized)
                {
                    var notFound = new @this(varName, null, null, Parent) { Context = context };
                    notFound.IsInitialized = false;
                    return notFound;
                }
                return resolved;
            }
            // Partial — interpolate into a fresh value but keep slot Name + alias state from `this`.
            var interpolated = context.Variable.Resolve(strVal);
            var transient = new @this(Name, interpolated, _type, Parent) { Context = context };
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
            var walked = WalkContainerVars(raw, context);
            // A wire-shaped dict IS a serialized Data — reconstruct it (value + type as a
            // whole) rather than wrapping the dict as a Data value, which would mislabel
            // the type as `object` and lose the inner value's real type.
            @this transient = IsWireShape(walked)
                ? FromWireShape(AsRawWireDict(walked)!, Name, context)
                : new @this(Name, walked, _type, Parent) { Context = context };
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
        raw is IList<object?> || raw is IDictionary<string, object?> || raw is app.type.dict.@this;

    // A native dict and a raw string-keyed dict both read out as a raw
    // Dictionary<string,object?> at the wire-shape boundary (the native dict
    // keeps Data-keyed entries in memory; here we want the raw read-out form).
    private static IDictionary<string, object?>? AsRawWireDict(object? raw) => raw switch
    {
        IDictionary<string, object?> d => d,
        app.type.dict.@this nd => nd.ToRaw(),
        _ => null,
    };

    // A dict carrying the canonical Data wire shape — a `value` slot paired with a
    // structured `type` entity. Such an object IS a serialized Data, so binding it to a
    // Data slot must reconstruct the Data (value + type as a whole), not nest the dict.
    internal static bool IsWireShape(object? raw)
        => AsRawWireDict(raw) is { } d && d.ContainsKey("value") && d.ContainsKey("type");

    // Reconstruct a Data from its wire shape ({name?, value, type}). The value is set
    // as a whole under its real type; a nested wire-shaped value is itself a Data. The
    // slot's name (not the wire dict's) is used so the value's identity is its content.
    internal static @this FromWireShape(IDictionary<string, object?> wire, string name, actor.context.@this? context)
    {
        object? rawValue = wire.TryGetValue("value", out var v) ? v : null;
        object? innerValue = IsWireShape(rawValue)
            ? FromWireShape(AsRawWireDict(rawValue)!, "", context)
            : rawValue;
        type? typeEntity = wire.TryGetValue("type", out var t) ? TypeFromWire(t, context) : null;
        return new @this(name, innerValue, typeEntity) { Context = context };
    }

    // Build a type entity from its wire form — a bare name string ("text") or the
    // structured {name, kind?, strict?} dict.
    private static type? TypeFromWire(object? t, actor.context.@this? context)
    {
        // A structured type wire ({name, kind?, strict?}) round-trips as a native
        // dict now — read it out raw so the IDictionary case below covers both.
        if (t is app.type.dict.@this ndType) t = ndType.ToRaw();
        switch (t)
        {
            case string s when !string.IsNullOrWhiteSpace(s):
                return type.Create(s, context: context);
            case IDictionary<string, object?> td when td.TryGetValue("name", out var nm) && nm != null:
                string? kind = td.TryGetValue("kind", out var k) ? k?.ToString() : null;
                bool strict = td.TryGetValue("strict", out var st) && st is bool b && b;
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
    private static object? WalkContainerVars(object? raw, actor.context.@this context)
    {
        if (raw is IList<object?> list) return WalkList(list, context);
        if (raw is app.type.dict.@this nativeDict) return WalkNativeDict(nativeDict, context);
        if (raw is IDictionary<string, object?> dict) return WalkDict(dict, context);
        return raw;
    }

    // Cycle protection for AsT_Impl. Tracks the raw %-containing strings currently being
    // resolved within the current async flow. When a recursive call sees a string already
    // in the set, it returns an error — no stack overflow on %a%↔%b% or %x%="%x%" graphs.
    //
    // AsyncLocal (not ThreadStatic) so the invariant "this state is per-resolution-stack"
    // holds even if a future await is introduced into the resolve chain. The chain is
    // currently sync, so the finally always clears before any caller awaits — but the
    // primitive shouldn't depend on that staying true.
    //
    // ResolveDepthLimit caps recursion for *expanding* cycles where the strings differ at each
    // level (e.g. %a%="X-%b%", %b%="Y-%a%" produces "X-%b%" → "X-Y-%a%" → "X-Y-X-%b%" → …).
    // The HashSet alone misses these because every recursion produces a new string. The depth
    // limit is well above any legitimate chain (real handler chains are 1–5 deep; matrix tests
    // exercise 5 levels — see AsT_DeepChain_5Levels_ResolvesCorrectly).
    private const int ResolveDepthLimit = 32;
    private static readonly AsyncLocal<HashSet<string>?> _resolvingValues = new();

    private @this<T> AsT_Impl<T>(object? raw, actor.context.@this? context)
    {
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
        if (raw is string strVal && strVal.Contains('%') && context?.Variable != null)
        {
            var resolving = _resolvingValues.Value;
            var isCycleRoot = resolving == null;
            if (isCycleRoot)
            {
                resolving = new HashSet<string>(StringComparer.Ordinal);
                _resolvingValues.Value = resolving;
            }

            // Cycle: an outer frame is already resolving this exact string. Don't Remove
            // on the cycle path — the outer frame still owns the entry.
            if (!resolving!.Add(strVal))
            {
                return @this<T>.FromError(new ServiceError(
                    $"Cyclic %var% reference detected while resolving '{strVal}'.",
                    "VariableResolutionCycle", 400));
            }

            try
            {
                // Depth-bound for expanding chains (each level produces a new string).
                if (resolving.Count > ResolveDepthLimit)
                {
                    return @this<T>.FromError(new ServiceError(
                        $"Variable resolution exceeded depth limit ({ResolveDepthLimit}) at '{strVal}'.",
                        "ResolveDepthExceeded", 400));
                }

                if (TryFullVarMatch(strVal, out var varName))
                {
                    var resolved = context.Variable.Get(varName);
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
                    // Stored values are values, not expressions. Type-convert only — never
                    // re-scan the resolved value's text for further %var% references. Calling
                    // on `resolved` (the live variable) preserves identity: WrapAs sees `resolved`
                    // as `this` and propagates Name + Properties + event lists.
                    return resolved.AsT_Convert<T>(resolved.Value, context);
                }
                // Partial match — interpolate once. The result is the final value; embedded
                // %var% inside the substituted text is opaque payload (matches mainstream
                // language semantics: assignment evaluates once, stored value is opaque).
                var interpolated = context.Variable.Resolve(strVal);
                return AsT_Convert<T>(interpolated, context);
            }
            finally
            {
                resolving.Remove(strVal);
                if (isCycleRoot) _resolvingValues.Value = null;
            }
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
        if (context != null && IsWalkableContainer(raw) && !IsActionDestination(typeof(T)) && !IsSelfResolvingParams(typeof(T)))
            return WrapAs<T>(WalkContainerVars(raw, context), context);

        // T has static Resolve(string, Context.@this) — Path-style domain types. Done before
        // the variance/wrap path because Resolve produces a fresh T from a string, not a
        // cast of an existing value.
        var staticResolved = TryStaticResolve<T>(raw, context);
        if (staticResolved != null) return staticResolved;

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
    private @this<T> AsT_Convert<T>(object? raw, actor.context.@this? context)
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
    private @this<T>? TryStaticResolve<T>(object? raw, actor.context.@this? context)
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
    /// Identity-preserving wrap. Applies the four As&lt;T&gt; rules (architect/v1/plan.md §Phase 2):
    ///   1. Same-type fast path  — `this` is already @this&lt;T&gt; with .Value of T → return `this`.
    ///   2. Variance fast path   — value is castable to T per IsPlangAssignable → new @this&lt;T&gt;,
    ///                              .Value cast-only (ref shared), state aliased from `this`.
    ///   3. Cross-type with conv — value can't satisfy T as-is → new @this&lt;T&gt; with converted
    ///                              .Value, state aliased from `this`. T=IEnumerable delegates to
    ///                              AsEnumerable so the string-not-iterable rule has one source.
    ///   4. Conversion failure   — FromError sentinel; no aliasing.
    ///
    /// Caller passes the substituted/walked `value` separately because raw value is what we wrap,
    /// while `this` is the canonical Data whose Name + Properties + event lists we propagate.
    /// </summary>
    private @this<T> WrapAs<T>(object? value, actor.context.@this? context)
    {
        // Rule 1 — same-type fast path. If `this` is already Data<T> AND its raw value is T,
        // return `this`. No allocation, full identity (Name, Properties, events all native).
        // Note: for action-destination carve-out the value may have been converted; but that
        // path enters here with raw, not converted, so we still match correctly.
        if (this is @this<T> sameTyped && sameTyped._value is T)
            return sameTyped;

        // Rule 2 — variance fast path. `value` is already a T (no conversion needed) but `this`
        // is not Data<T> (e.g. plain Data, or Data<U> for U:T). Construct a new Data<T> sharing
        // .Value by ref (cast-only) and alias Properties + events from `this`.
        if (value is T fast && IsPlangAssignable(typeof(T), value.GetType()))
            return ConstructWrap<T>(fast, context);

        // Null arrives here only when raw was null (or substitution produced null). Construct a
        // not-initialized Data<T> with default value, aliased state from `this`.
        if (value == null)
            return ConstructWrap<T>(default, context);

        // Rule 3 — cross-type with conversion. T=IEnumerable (the non-generic interface itself,
        // not arbitrary subtypes) delegates to AsEnumerable so the string-not-iterable carve-out
        // applies (a string `value` becomes a one-element array, not a char enumeration). For
        // typed collections like List<LlmMessage>, fall through to TypeMapping which knows how
        // to deserialize element-by-element.
        if (typeof(T) == typeof(System.Collections.IEnumerable))
        {
            // value != null guaranteed above, so the AsEnumerable contract collapses to:
            // iterable → use as-is; non-iterable → wrap as one-element array.
            object convertedEnum = IsPlangIterable(value) ? value : new[] { value };
            return ConstructWrap<T>((T?)convertedEnum, context);
        }

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
    private @this<T> ConstructWrap<T>(T? value, actor.context.@this? context)
    {
        var wrapped = new @this<T>(Name, value, _type, Parent) { Context = context };
        wrapped.Properties = Properties;
        wrapped.OnCreate   = OnCreate;
        wrapped.OnChange   = OnChange;
        wrapped.OnDelete   = OnDelete;
        return wrapped;
    }

    private static List<object?> WalkList(IList<object?> list, actor.context.@this context)
    {
        var result = new List<object?>(list.Count);
        foreach (var item in list)
            result.Add(SubstitutePrimitive(item, context));
        return result;
    }

    private static Dictionary<string, object?> WalkDict(IDictionary<string, object?> dict, actor.context.@this context)
    {
        var result = new Dictionary<string, object?>(dict.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in dict)
            result[kvp.Key] = SubstitutePrimitive(kvp.Value, context);
        return result;
    }

    // Walk a native dict's entry values for nested %var%, preserving dict-ness so
    // downstream navigation still hits the value type. A fresh dict is built — the
    // source is never mutated (mirrors WalkDict's fresh-container contract).
    private static app.type.dict.@this WalkNativeDict(app.type.dict.@this dict, actor.context.@this context)
    {
        var result = new app.type.dict.@this { Context = context };
        foreach (var entry in dict.Entries)
            result.Set(new @this(entry.Name, SubstitutePrimitive(entry.Value, context)));
        return result;
    }

    // Shape contract: WalkList / WalkDict / SubstitutePrimitive only match the typed-generic
    // shapes IList<object?> / IDictionary<string, object?>. A non-generic IList (ArrayList)
    // or IDictionary (Hashtable) passes through to the fall-through and is returned as-is —
    // no %var% substitution. JSON ingestion is normalized to the typed forms via
    // UnwrapJsonElement / UnwrapNewtonsoftToken upstream, so this is safe in practice.
    private static object? SubstitutePrimitive(object? value, actor.context.@this context)
    {
        if (value == null) return null;

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
                var resolved = context.Variable.Get(varName);
                return resolved?.IsInitialized == true && resolved.Value != null
                    ? resolved.Value
                    : (object?)s;
            }
            return context.Variable.Resolve(s);
        }

        if (value is IList<object?> innerList) return WalkList(innerList, context);
        if (value is app.type.dict.@this innerNativeDict) return WalkNativeDict(innerNativeDict, context);
        if (value is IDictionary<string, object?> innerDict) return WalkDict(innerDict, context);

        // Non-recursion guards: don't walk into Data, Action templates, or typed Action lists.
        // Action templates retain raw %var% for deferred resolution at their own dispatch.
        if (value is @this) return value;
        if (value is global::app.goal.steps.step.actions.action.@this) return value;
        if (value is global::System.Collections.Generic.IEnumerable<global::app.goal.steps.step.actions.action.@this>) return value;

        return value;
    }

    /// <summary>
    /// Builds a named goal-call parameter from a raw param value. A full-match
    /// <c>%var%</c> clones the live variable's Data under the parameter name — value,
    /// type and signature shared by reference, no JSON round-trip — so signed/typed
    /// values survive the call intact. Anything else (literal, partial interpolation,
    /// container) resolves through the normal substitution and wraps as a fresh Data.
    /// </summary>
    public static @this ResolveParameter(string name, object? rawValue, actor.context.@this context)
    {
        if (rawValue is string s && TryFullVarMatch(s, out var varName))
        {
            var live = context.Variable.Get(varName);
            if (live != null && live.IsInitialized)
                return live.ShallowClone(name);
        }
        return new @this(name, SubstitutePrimitive(rawValue, context));
    }

    // GoalCall resolves its own parameters (GoalCall.Convert → ResolveParameter) so a
    // full-match %var% param clones the live Data — signature/type preserved — instead of
    // being flattened to a bare value by the generic container walk.
    private static bool IsSelfResolvingParams(System.Type t) =>
        t == typeof(global::app.goal.GoalCall);

    private static bool IsActionDestination(System.Type t)
    {
        var actionType = typeof(global::app.goal.steps.step.actions.action.@this);
        if (t == actionType) return true;
        return typeof(global::System.Collections.Generic.IEnumerable<global::app.goal.steps.step.actions.action.@this>).IsAssignableFrom(t);
    }

    /// <summary>
    /// Creates a deep clone of this Data. Value is deep-cloned, metadata is preserved.
    /// The natural boolean meaning of this Data.
    /// Follows common language conventions: null, false, 0, "" are falsy. Everything else is truthy.
    /// </summary>
    public virtual bool ToBoolean()
    {
        if (!IsInitialized) return false;
        var val = Value;
        if (val == null) return false;
        if (val is bool b) return b;
        if (val is string s) return s.Length > 0;
        if (val is int i) return i != 0;
        if (val is long l) return l != 0;
        if (val is double d) return d != 0;
        if (val is float f) return f != 0;
        if (val is decimal dec) return dec != 0;
        if (val is short sh) return sh != 0;
        if (val is byte by) return by != 0;
        return true;
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
        if (IsInitialized && Value is IBooleanResolvable resolvable)
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
        var clone = new @this(newName, _value, _type)
        {
            Error = Error,
            Handled = Handled,
            Returned = Returned,
            ReturnDepth = ReturnDepth,
            Warnings = Warnings != null ? new List<Info>(Warnings) : null,
            Signature = Signature,
            Properties = Properties
        };
        clone._valueFactory = _valueFactory;
        // Carry the raw backing so a lazily-read (raw-backed) value stays lazy through
        // the clone — without this, RawUntouched would be false and the next .Value
        // read would materialize null. _type already rode in via the constructor.
        clone._raw = _raw;
        clone.Context = _context;
        // A bare `object` type with no kind is the "unknown" sentinel a polymorphic slot
        // stamps — the value's real CLR type is the truth, so let it derive rather than
        // carrying a stale `object` label into the new binding. A genuine shape type
        // (object/json, kind set) and raw-backed values keep their declared type.
        if (!clone.RawUntouched && clone.Type is { IsNull: false, Name: "object", Kind: null })
            clone.Type = type.Null;
        return clone;
    }

    /// <summary>
    /// Deep-clones this Data including its value. Events are intentionally not copied —
    /// clones that go through Variables.Set() get events wired at storage time.
    /// </summary>
    public virtual @this Clone()
    {
        var clonedValue = _value.DeepClone();
        var clone = new @this(Name, clonedValue, _type)
        {
            Error = Error,
            Handled = Handled,
            Returned = Returned,
            ReturnDepth = ReturnDepth,
            Warnings = Warnings != null ? new List<Info>(Warnings) : null,
            Signature = Signature,
            Properties = Properties.Clone()
        };
        clone._valueFactory = _valueFactory;
        clone.Context = _context;
        return clone;
    }

    public override string ToString() =>
        Success ? _value?.ToString() ?? "(null)" : $"Error: {Error?.Message}";

    private const int MaxJsonDepth = 128;

    /// <summary>
    /// JSON-roundtrip deep clone for snapshotting mutable refs (Lists/Dicts/POCOs) where
    /// `Force.DeepCloner` would hang on cyclic runtime types. Honors `[JsonIgnore]` so
    /// Goal↔Step↔Action cycles break. Uses CamelCase keys (matches `.pr` files, traces, viewer)
    /// and unwraps the resulting `JsonElement` graph back to CLR primitives + Dictionary/List
    /// so callers don't have to.
    ///
    /// Throws on serialization failure — call sites that want a fallback (e.g. alias-mode
    /// degradation) wrap the call in their own try/catch.
    /// </summary>
    // Per-Data static — symmetric write+read for snapshot cloning. Pure config bag,
    // Data is allocated frequently so static-readonly avoids per-instance allocation.
    // Stage 27 disperse-from-Json target.
    private static readonly JsonSerializerOptions _snapshotClone = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    internal static object? SnapshotClone(object source)
    {
        var json = JsonSerializer.Serialize(source, _snapshotClone);
        var deserialized = JsonSerializer.Deserialize<object?>(json, _snapshotClone);
        return UnwrapJsonElement(deserialized);
    }

    internal static object? UnwrapJsonElement(object? value, int depth = 0)
    {
        if (depth > MaxJsonDepth)
            throw new InvalidOperationException($"JSON nesting exceeds maximum depth ({MaxJsonDepth})");

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => UnwrapJsonNumber(element),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,
                JsonValueKind.Object => UnwrapJsonObject(element, depth),
                JsonValueKind.Array => UnwrapJsonArray(element, depth),
                _ => element
            };
        }

        // Convert Newtonsoft JToken to CLR types (v1 runtime compatibility shim).
        // Detected by namespace so App has no Newtonsoft import.
        if (value != null && value.GetType().Namespace == "Newtonsoft.Json.Linq")
        {
            return UnwrapNewtonsoftToken(value, depth);
        }

        // System.Text.Json.Nodes DOM types (JsonObject, JsonArray, JsonValue) flow through
        // variable.set when the source value is parsed as a mutable JSON DOM — the
        // builder's `set %messages% = [{...}], type=json` produces these. Unwrap to the
        // canonical List<object?>/Dict<string,object?> the walker recognises so nested
        // `%var%` strings inside JSON values get substituted on read (the alternative is
        // the LLM seeing literal `%goalForLlm%` in its user message — which is the
        // regression that surfaced after the App→app rename merge).
        if (value is System.Text.Json.Nodes.JsonNode jsonNode)
        {
            // Round-trip via the JsonElement path — keeps numeric / null / bool semantics
            // identical to the System.Text.Json branch above and reuses the existing
            // UnwrapJsonObject/UnwrapJsonArray walkers without duplicating their logic.
            using var doc = System.Text.Json.JsonDocument.Parse(jsonNode.ToJsonString());
            return UnwrapJsonElement(doc.RootElement, depth);
        }

        return value;
    }

    /// <summary>
    /// Converts a Newtonsoft JToken to plain CLR types without importing Newtonsoft.
    /// JValue → extract underlying CLR value via reflection.
    /// JObject/JArray → round-trip through JSON string → System.Text.Json.
    /// </summary>
    private static object? UnwrapNewtonsoftToken(object value, int depth)
    {
        var typeName = value.GetType().Name;

        // JValue holds a CLR primitive in its Value property
        if (typeName == "JValue")
        {
            var underlying = value.GetType().GetProperty("Value")?.GetValue(value);
            return underlying;
        }

        // JObject/JArray → serialize to JSON string, re-parse with System.Text.Json
        var json = value.ToString();
        if (string.IsNullOrEmpty(json)) return null;

        using var doc = JsonDocument.Parse(json);
        return UnwrapJsonElement(doc.RootElement, depth);
    }

    // A json object narrows to the native `dict` value type — collections hold
    // Data end to end, so each property value is wrapped in a named Data (which
    // keeps its own type-tag and signature) rather than decomposed to raw CLR.
    private static app.type.dict.@this UnwrapJsonObject(JsonElement element, int depth)
    {
        var dict = new app.type.dict.@this();
        foreach (var prop in element.EnumerateObject())
            dict.Set(new @this(prop.Name, UnwrapJsonElement(prop.Value, depth + 1)));
        return dict;
    }

    private static List<object?> UnwrapJsonArray(JsonElement element, int depth)
    {
        var list = new List<object?>();
        foreach (var item in element.EnumerateArray())
        {
            list.Add(UnwrapJsonElement(item, depth + 1));
        }
        return list;
    }

    private static object UnwrapJsonNumber(JsonElement element)
    {
        if (element.TryGetInt64(out var l)) return l;
        // Bare decimal-point literal → double by default (decimal is opt-in via
        // `as number/decimal`), matching universal language convention.
        return element.GetDouble();
    }

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
public class @this<T> : @this
{
    public new T? Value
    {
        get => base.Value is T typed ? typed : GetValue<T>();
        set => base.Value = value;
    }

    public @this(string name = "", T? value = default, type? type = null, @this? parent = null)
        : base(name, value, type, parent) { }

    public static @this<T> Ok(T value, type? type = null) => new("", value, type);
    public new static @this<T> FromError(IError error) => new() { Error = error };

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
        var copy = new @this<T>(source.Name, source.Value is T t ? t : default, source.Type)
        {
            Error = source.Error,
            Handled = source.Handled,
            Returned = source.Returned,
            ReturnDepth = source.ReturnDepth,
            Warnings = source.Warnings != null ? new List<Info>(source.Warnings) : null,
            Signature = source.Signature,
            Properties = source.Properties,
            Snapshot = source.Snapshot,
        };
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
/// Dynamic Data that computes its value on access.
/// </summary>
public class DynamicData : @this
{
    private readonly Func<object?> _valueFactory;

    public DynamicData(string name, Func<object?> valueFactory, type? type = null)
        : base(name, null, type)
    {
        _valueFactory = valueFactory;
    }

    public override object? Value => _valueFactory();
}

