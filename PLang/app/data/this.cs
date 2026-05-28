using System.Text.Json;
using System.Text.Json.Serialization;
using Force.DeepCloner;
using app.Attributes;
using app;
using app.channels.serializers;
using app.errors;
using app.actor.context;
using app.Utils;

namespace app.data;

/// <summary>
/// PLang type descriptor. Value is a type string: "string", "long", "text/markdown", "image/jpeg", etc.
/// CLR type is derived on the fly via TypeMapping.
/// </summary>
[System.ComponentModel.TypeConverter(typeof(global::app.data.Converter))]
public sealed class type
{
    public string Value { get; }

    [JsonIgnore]
    internal actor.context.@this? Context { get; set; }

    public type(string value) { Value = value; }

    /// <summary>
    /// Derive CLR type: navigate through context to App.Types, fall back to static TypeMapping.
    /// </summary>
    public System.Type? ClrType => Context?.App.Types.Clr(Value) ?? AppTypes.GetPrimitiveOrMime(Value);

    /// <summary>
    /// Kind of this type value (e.g. "image", "text"). Null for PLang type names like "string".
    /// </summary>
    public string? Kind => Context?.App.Formats.KindOf(Value);

    /// <summary>
    /// Whether content of this type benefits from compression.
    /// </summary>
    public bool Compressible => Kind != null && (Context?.App.Formats.Compressible(Kind) ?? false);

    public static type String => new("string");
    public static type Int => new("int");
    public static type Long => new("long");
    public static type Double => new("double");
    public static type Bool => new("bool");
    public static type DateTime => new("datetime");
    public static type Object => new("object");

    /// <summary>
    /// Factory from MIME type (used by file handlers).
    /// </summary>
    public static type FromMime(string mimeType) => new(mimeType);

    /// <summary>
    /// Factory from PLang type name.
    /// </summary>
    public static type FromName(string typeName) => new(typeName);

    public override string ToString() => Value;

    /// <summary>
    /// Converts a raw string value to the appropriate object based on this type.
    /// Returns null if no conversion is needed or possible.
    /// Called lazily on first navigation into a string-typed Data.
    /// </summary>
    public object? Convert(string raw)
    {
        return Value.ToLowerInvariant() switch
        {
            "json" => JsonSerializer.Deserialize<Dictionary<string, object?>>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }),
            _ => AppTypes.TryConvertTo(raw, ClrType ?? typeof(object)).Value
        };
    }
}

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
    private type? _type;
    private actor.context.@this? _context;

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
    public actor.context.@this? Context
    {
        get => _context;
        set
        {
            _context = value;
            if (_type != null) _type.Context = value;
            if (_value is modules.IContext contextual && value != null)
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
            return _value;
        }
        set
        {
            _value = UnwrapJsonElement(value);
            _valueFactory = null;
            Updated = System.DateTime.UtcNow;
            IsInitialized = true;
            _type = null;
            if (_value is modules.IContext contextual && _context != null)
                contextual.Context = _context;
            // Data owns OnChange — fires whenever the wrapped value mutates.
            // Constructors set _value directly and bypass this. SetValueDirect also bypasses.
            FireOnChange(this);
        }
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
    /// Lazily converts the value based on its Type.
    /// Called on first navigation into the value — if the value is a string
    /// and the Type knows how to convert it, replaces the value with the converted object.
    /// Only converts once — subsequent accesses use the converted value directly.
    /// </summary>
    public void ConvertValue()
    {
        if (_value is not string raw || _type == null) return;
        var converted = _type.Convert(raw);
        if (converted != null)
            SetValueDirect(converted);
    }

    /// <summary>
    /// Updates _value without triggering Value setter side effects (no type clearing, no unwrap).
    /// Used by RehydrateNestedData to replace a dictionary with a reconstructed Data object
    /// without losing the outer Type.
    /// </summary>
    private void SetValueDirect(object? value)
    {
        _value = value;
        Updated = System.DateTime.UtcNow;
        IsInitialized = true;
    }

    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(global::app.data.Json))]
    [Out, Store]
    public type? Type
    {
        get
        {
            if (_type != null) return _type;
            if (_value == null) return null;
            var typeName = _context?.App.Types.Name(_value.GetType())
                           ?? AppTypes.GetPrimitiveName(_value.GetType())
                           ?? _value.GetType().Name.ToLowerInvariant();
            var derived = new type(typeName);
            derived.Context = _context;
            _type = derived;
            return _type;
        }
        set
        {
            _type = value;
            if (value != null && _context != null) value.Context = _context;
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
    /// Walks _value, substitutes %variable% references via context.Variables,
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
        var ctx = context ?? _context;
        var raw = Value; // factory-resolved if any; never %var% substituted
        return AsT_Impl<T>(raw, ctx);
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
            return global::app.data.@this.FromError(new global::app.errors.ServiceError(
                "Data.As(typeName) requires a non-empty type name.", "InvalidTypeName", 400));

        var ctx = context ?? _context;
        var clr = ctx?.App.Types.Clr(typeName) ?? AppTypes.GetPrimitiveOrMime(typeName);
        if (clr == null)
            return global::app.data.@this.FromError(new global::app.errors.ServiceError(
                $"No PLang type registered under name '{typeName}'.", "UnknownType", 400));

        var raw = Value;
        var converted = raw is string s
            ? AppTypes.TryConvertTo(s, clr).Value
            : AppTypes.ConvertTo(raw, clr);
        return new @this(Name, converted, new type(typeName), Parent) { Context = ctx };
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
        var ctx = context ?? _context;
        var raw = Value;

        if (raw is string strVal && strVal.Contains('%') && ctx?.Variables != null)
        {
            if (TryFullVarMatch(strVal, out var varName))
            {
                var resolved = ctx.Variables.Get(varName);
                if (resolved == null || !resolved.IsInitialized)
                {
                    var notFound = new @this(varName, null, null, Parent) { Context = ctx };
                    notFound.IsInitialized = false;
                    return notFound;
                }
                return resolved;
            }
            // Partial — interpolate into a fresh value but keep slot Name + alias state from `this`.
            var interpolated = ctx.Variables.Resolve(strVal);
            var transient = new @this(Name, interpolated, _type, Parent) { Context = ctx };
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
        if (ctx != null && IsWalkableContainer(raw))
        {
            var walked = WalkContainerVars(raw, ctx);
            var transient = new @this(Name, walked, _type, Parent) { Context = ctx };
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
        raw is IList<object?> || raw is IDictionary<string, object?>;

    // Walk lists/dicts to substitute nested %var% references. Always returns a fresh
    // container for IList<object?> / IDictionary<string,object?> (WalkList/WalkDict
    // allocate). Strings are NOT handled here — full-match vs. partial-interpolation
    // semantics differ between AsCanonical (returns live var Data) and AsT_Impl (recurses
    // typed), so each owns its own string path.
    private static object? WalkContainerVars(object? raw, actor.context.@this ctx)
    {
        if (raw is IList<object?> list) return WalkList(list, ctx);
        if (raw is IDictionary<string, object?> dict) return WalkDict(dict, ctx);
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

    private @this<T> AsT_Impl<T>(object? raw, actor.context.@this? ctx)
    {
        // Action-destination carve-out: when T is or contains Action.@this, sub-actions
        // hold raw %var% for deferred resolution at their own dispatch time. Skip the walk
        // and convert raw straight through TypeMapping. BUT — only when raw is already a
        // typed action structure. If raw is itself a `%var%` reference (e.g. `actions=%stepResult.actions%`),
        // we still have to resolve the variable to GET the action list before the carve-out
        // applies; otherwise the literal string is handed to TypeConverter which can't
        // convert "%var%" → StepActions and the build dies with "Cannot convert String to this".
        if (IsActionDestination(typeof(T))
            && !(raw is string actStr && actStr.Contains('%') && ctx?.Variables != null))
            return WrapAs<T>(raw, ctx);

        // Raw-name carve-out: types like app.variables.Variable want the literal slot
        // string — `%x%` means "the variable named x" not "x's value". Bypass the
        // %var% substitution branch and dispatch to T.Resolve(raw, ctx) directly.
        // Variable.Resolve strips the % and produces { Name="x" } regardless of whether
        // x is initialized — symmetric for both `%x%` and bare `x` slot forms.
        if (raw is string rawNameStr && ctx != null
            && typeof(app.variables.IRawNameResolvable).IsAssignableFrom(typeof(T)))
        {
            var resolveMethod = ResolveMethodCache.GetOrAdd(typeof(T), t =>
                t.GetMethod("Resolve",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null, new[] { typeof(string), typeof(actor.context.@this) }, null));
            if (resolveMethod != null)
            {
                var resolvedObj = resolveMethod.Invoke(null, new object[] { rawNameStr, ctx });
                if (resolvedObj is T result)
                    return ConstructWrap<T>(result, ctx);
            }
        }

        // String with %var% — substitute first, BEFORE fast paths. Without this ordering,
        // T=object would always match `raw is T` and short-circuit substitution.
        if (raw is string strVal && strVal.Contains('%') && ctx?.Variables != null)
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
                    var resolved = ctx.Variables.Get(varName);
                    if (resolved == null || !resolved.IsInitialized)
                    {
                        // Unset var — propagate the variable's name so handler diagnostics see it.
                        // Mark as not-initialized so callers can detect the difference between
                        // "value is null" and "var doesn't exist".
                        var notFound = new @this<T>(varName, default, null, Parent) { Context = ctx };
                        notFound.IsInitialized = false;
                        return notFound;
                    }
                    if (!resolved.Success)
                        return @this<T>.FromError(resolved.Error!);
                    // Stored values are values, not expressions. Type-convert only — never
                    // re-scan the resolved value's text for further %var% references. Calling
                    // on `resolved` (the live variable) preserves identity: WrapAs sees `resolved`
                    // as `this` and propagates Name + Properties + event lists.
                    return resolved.AsT_Convert<T>(resolved.Value, ctx);
                }
                // Partial match — interpolate once. The result is the final value; embedded
                // %var% inside the substituted text is opaque payload (matches mainstream
                // language semantics: assignment evaluates once, stored value is opaque).
                var interpolated = ctx.Variables.Resolve(strVal);
                return AsT_Convert<T>(interpolated, ctx);
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
        if (ctx != null && IsWalkableContainer(raw) && !IsActionDestination(typeof(T)))
            return WrapAs<T>(WalkContainerVars(raw, ctx), ctx);

        // T has static Resolve(string, Context.@this) — Path-style domain types. Done before
        // the variance/wrap path because Resolve produces a fresh T from a string, not a
        // cast of an existing value.
        if (raw is string srStr && ctx != null && raw is not T)
        {
            var resolveMethod = ResolveMethodCache.GetOrAdd(typeof(T), t =>
                t.GetMethod("Resolve",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null, new[] { typeof(string), typeof(actor.context.@this) }, null));
            if (resolveMethod != null)
            {
                var (resolvedObj, resolveError) = InvokeResolve<T>(resolveMethod, srStr, ctx);
                if (resolveError != null) return resolveError;
                if (resolvedObj is T result)
                    return ConstructWrap<T>(result, ctx);
            }
        }

        // No more substitution to do — `this` is the canonical. Apply identity-preserving
        // wrap rules (same-type fast path → variance → cross-type with conversion).
        return WrapAs<T>(raw, ctx);
    }

    /// <summary>
    /// Type-conversion tail of <see cref="AsT_Impl"/> — no substitution. Used after a slot's
    /// %var% has already been resolved (full-match Variables.Get or partial-match interpolation):
    /// the value is final and its string content must NOT be scanned for further %var% references.
    /// Keeps the static-Resolve(string) carve-out for Path-style domain types, then delegates to
    /// WrapAs for identity-preserving wrap + conversion.
    /// </summary>
    private @this<T> AsT_Convert<T>(object? raw, actor.context.@this? ctx)
    {
        if (raw is string srStr && ctx != null && raw is not T)
        {
            var resolveMethod = ResolveMethodCache.GetOrAdd(typeof(T), t =>
                t.GetMethod("Resolve",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null, new[] { typeof(string), typeof(actor.context.@this) }, null));
            if (resolveMethod != null)
            {
                var (resolvedObj, resolveError) = InvokeResolve<T>(resolveMethod, srStr, ctx);
                if (resolveError != null) return resolveError;
                if (resolvedObj is T result)
                    return ConstructWrap<T>(result, ctx);
            }
        }
        return WrapAs<T>(raw, ctx);
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
        System.Reflection.MethodInfo resolveMethod, string raw, actor.context.@this ctx)
    {
        try
        {
            return (resolveMethod.Invoke(null, new object[] { raw, ctx }), null);
        }
        catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException != null)
        {
            var inner = tie.InnerException;
            if (inner is global::app.types.path.scheme.SchemeNotRegistered snr)
                return (null, @this<T>.FromError(new global::app.errors.Error(snr.Message, "SchemeNotRegistered", 400)
                {
                    FixSuggestion = $"Register a factory for scheme '{snr.Scheme}' via app.Types.Scheme.Register, or use a bare/file:// path.",
                }));
            return (null, @this<T>.FromError(new global::app.errors.Error(inner.Message, "ResolveFailed", 400)));
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
    private @this<T> WrapAs<T>(object? value, actor.context.@this? ctx)
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
            return ConstructWrap<T>(fast, ctx);

        // Null arrives here only when raw was null (or substitution produced null). Construct a
        // not-initialized Data<T> with default value, aliased state from `this`.
        if (value == null)
            return ConstructWrap<T>(default, ctx);

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
            return ConstructWrap<T>((T?)convertedEnum, ctx);
        }

        var (converted, error) = AppTypes.TryConvertTo(value, typeof(T), ctx);
        if (error != null)
            return @this<T>.FromError(error);
        return ConstructWrap<T>((T?)converted, ctx);
    }

    /// <summary>
    /// Builds a new Data&lt;T&gt; that takes `this` as the canonical: Name + Type + Parent are
    /// inherited; Properties + the three event lists are aliased by reference (shared list refs
    /// so subscribers and metadata mutations are visible through both source and view).
    /// </summary>
    private @this<T> ConstructWrap<T>(T? value, actor.context.@this? ctx)
    {
        var wrapped = new @this<T>(Name, value, _type, Parent) { Context = ctx };
        wrapped.Properties = Properties;
        wrapped.OnCreate   = OnCreate;
        wrapped.OnChange   = OnChange;
        wrapped.OnDelete   = OnDelete;
        return wrapped;
    }

    private static List<object?> WalkList(IList<object?> list, actor.context.@this ctx)
    {
        var result = new List<object?>(list.Count);
        foreach (var item in list)
            result.Add(SubstitutePrimitive(item, ctx));
        return result;
    }

    private static Dictionary<string, object?> WalkDict(IDictionary<string, object?> dict, actor.context.@this ctx)
    {
        var result = new Dictionary<string, object?>(dict.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in dict)
            result[kvp.Key] = SubstitutePrimitive(kvp.Value, ctx);
        return result;
    }

    // Shape contract: WalkList / WalkDict / SubstitutePrimitive only match the typed-generic
    // shapes IList<object?> / IDictionary<string, object?>. A non-generic IList (ArrayList)
    // or IDictionary (Hashtable) passes through to the fall-through and is returned as-is —
    // no %var% substitution. JSON ingestion is normalized to the typed forms via
    // UnwrapJsonElement / UnwrapNewtonsoftToken upstream, so this is safe in practice.
    private static object? SubstitutePrimitive(object? value, actor.context.@this ctx)
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
                var resolved = ctx.Variables.Get(varName);
                return resolved?.IsInitialized == true && resolved.Value != null
                    ? resolved.Value
                    : (object?)s;
            }
            return ctx.Variables.Resolve(s);
        }

        if (value is IList<object?> innerList) return WalkList(innerList, ctx);
        if (value is IDictionary<string, object?> innerDict) return WalkDict(innerDict, ctx);

        // Non-recursion guards: don't walk into Data, Action templates, or typed Action lists.
        // Action templates retain raw %var% for deferred resolution at their own dispatch.
        if (value is @this) return value;
        if (value is global::app.goals.goal.steps.step.actions.action.@this) return value;
        if (value is global::System.Collections.Generic.IEnumerable<global::app.goals.goal.steps.step.actions.action.@this>) return value;

        return value;
    }

    private static bool IsActionDestination(System.Type t)
    {
        var actionType = typeof(global::app.goals.goal.steps.step.actions.action.@this);
        if (t == actionType) return true;
        return typeof(global::System.Collections.Generic.IEnumerable<global::app.goals.goal.steps.step.actions.action.@this>).IsAssignableFrom(t);
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
    public @this ShallowClone()
    {
        var clone = new @this(Name, _value, _type)
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
        clone.Context = _context;
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

    private static Dictionary<string, object?> UnwrapJsonObject(JsonElement element, int depth)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = UnwrapJsonElement(prop.Value, depth + 1);
        }
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
        if (element.TryGetDecimal(out var d)) return d;
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

