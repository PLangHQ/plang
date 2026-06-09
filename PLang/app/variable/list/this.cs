using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Force.DeepCloner;
using app.actor.context;

namespace app.variable.list;

/// <summary>
/// Thread-safe variable storage for App.
/// </summary>
public partial class @this
{
    // Symmetric write+read for snapshot cloning. Pure config bag — `static readonly`
    // is the Rule C exception class for stateless option holders. Stage 27 disperse-from-Json target.
    private static readonly System.Text.Json.JsonSerializerOptions _snapshotClone = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ConcurrentDictionary<string, data.@this> _variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<string, System.Threading.Tasks.ValueTask<data.@this>>> _navigables
        = new(StringComparer.OrdinalIgnoreCase);
    private actor.context.@this? _context;

    /// <summary>
    /// Registers a navigable mount: when <see cref="Get"/> resolves <c>name.X</c>
    /// and <c>name</c> isn't found in the regular variable scope, the resolver
    /// is called with the full path remainder (e.g. <c>"X"</c> or <c>"X.Y"</c>)
    /// and its result is returned. Used by Settings: each actor's Variables
    /// registers <c>"Settings"</c> with a resolver that delegates to
    /// <c>app.Settings.Get(path, this.Context)</c>. Generalises to any future
    /// non-Data navigable mount.
    /// </summary>
    public void RegisterNavigable(string name, Func<string, System.Threading.Tasks.ValueTask<data.@this>> resolver)
        => _navigables[name] = resolver;

    /// <summary>
    /// Per-type navigator registry for Data navigation. Delegates to the
    /// shared App-level instance — navigators are app-wide infrastructure;
    /// the property is exposed here so the canonical access path is
    /// <c>app.variable.navigator</c> alongside other variable concerns.
    /// </summary>
    [JsonIgnore]
    public navigator.list.@this Navigators => _context!.App.Navigator;

    /// <summary>
    /// Per-call parameter scopes. <see cref="Get"/> consults <c>Calls.Current</c> before
    /// falling back to the actor-shared dictionary — that's how goal-call parameters
    /// (e.g. <c>%!data%</c> on a goal channel) avoid racing across concurrent calls on
    /// the same actor.
    /// </summary>
    [JsonIgnore]
    public call.list.@this Calls { get; } = new();

    [JsonIgnore]
    internal actor.context.@this? Context
    {
        get => _context;
        set
        {
            _context = value;
            foreach (var data in _variables.Values)
                data.Context = value;
        }
    }

    /// <summary>
    /// Fires after a variable is rebound (existing name → new value). Carries (name, before, after).
    /// Collection-level event — fires for any name. Per-variable Data.OnChange still fires too.
    /// Used by Call.@this diff capture: subscribe in ctor, unsubscribe in DisposeAsync.
    /// </summary>
    public event Action<string, object?, object?>? OnSet;

    /// <summary>
    /// Fires when a name is created for the first time. Carries (name, value).
    /// </summary>
    public event Action<string, object?>? OnCreate;

    /// <summary>
    /// Fires when a name is removed.
    /// </summary>
    public event Action<string>? OnRemove;

    public @this()
    {
        // System variables seeded directly (Set is async now; simple names, no navigation,
        // no subscribers at construction — Context is wired later).
        _variables["Now"] = new data.DynamicData("Now", () => DateTimeOffset.Now, app.type.@this.DateTime);
        _variables["NowUtc"] = new data.DynamicData("NowUtc", () => DateTimeOffset.UtcNow, app.type.@this.DateTime);
        _variables["GUID"] = new data.DynamicData("GUID", () => Guid.NewGuid(), app.type.@this.FromName("guid"));
    }

    /// <summary>
    /// Stores a Data under its own Data.Name.
    /// Convenience wrapper — the name comes from value.Name.
    /// </summary>
    public System.Threading.Tasks.ValueTask<data.@this> Set(data.@this value) => Set(value.Name, value);

    /// <summary>
    /// Stores a value under the given name and returns the stored Data.
    /// Semantics by `value` type:
    ///  - `data.@this` → aliased under `name` as-is (no clone, no rename). The dictionary
    ///    key is the source of truth for lookups; `Data.Name` stays advisory — whatever the
    ///    producing handler set it to. Same object is reachable under both keys.
    ///  - non-Data → wrapped in a new `Data` named `name`. Existing entry, if any, is updated
    ///    in-place so readers holding the previous reference see the new value.
    /// For dot/bracket paths (e.g. "user.name"), the root Data is returned.
    /// Returns NotFound when the dot-path parent is absent or null.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<data.@this> Set(string name, object? value, app.type.@this? type = null)
    {
        name = CleanName(name);

        // Resolve variable references inside bracket indices: [idx] → [1]
        if (name.Contains('['))
            name = ResolveVariablesInPath(name);

        var rootName = GetRootName(name);

        // Simple case: no dot/bracket path — set the root variable directly
        if (rootName == name)
        {
            // If a Calls overlay is active (we're inside a forked flow — channel fire,
            // parallel foreach iteration, etc.), route the write into the overlay so
            // siblings can't see it. Reads cascade overlay → caller chain → underlying
            // dict, so subsequent gets here see the new value.
            var frame = Calls.Current;

            // Data value: replace under `name`. The binding's state (Properties + event
            // subscribers) follows the name onto the new Data — that's how
            // `--debug={"variables":[...]}` watches see every assignment, and how Properties
            // attached to a name survive a re-mint. In-place mutation of prev is wrong: a
            // Data may be aliased under multiple keys (e.g. Action stores the step result
            // both under its own name AND under "!data"), so mutating prev would bleed
            // across keys.
            if (value is data.@this dv)
            {
                dv.Context = _context;

                if (frame != null)
                {
                    var hadPrev = frame.TryGet(name, out var prevFrame);
                    if (hadPrev && !ReferenceEquals(prevFrame, dv))
                    {
                        dv.OnCreate = prevFrame.OnCreate;
                        dv.OnChange = prevFrame.OnChange;
                        dv.OnDelete = prevFrame.OnDelete;
                        var prevValue = prevFrame.Peek();
                        prevFrame.FireOnChange(dv);
                        frame.Set(name, dv);
                        OnSet?.Invoke(name, prevValue, dv.Peek());
                        return dv;
                    }
                    else if (!hadPrev)
                    {
                        dv.FireOnCreate();
                        frame.Set(name, dv);
                        OnCreate?.Invoke(name, dv.Peek());
                        return dv;
                    }
                    frame.Set(name, dv);
                    return dv;
                }

                if (_variables.TryGetValue(name, out var prev) && !ReferenceEquals(prev, dv))
                {
                    // Event subscribers follow the *name* across re-binding (so
                    // `--debug={"variables":[...]}` watches see every assignment, not just
                    // the first). Properties stay attached to the Data instance — they're
                    // result metadata (e.g. condition.if's branchIndex), not binding metadata.
                    dv.OnCreate = prev.OnCreate;
                    dv.OnChange = prev.OnChange;
                    dv.OnDelete = prev.OnDelete;
                    var prevValue = prev.Peek();
                    prev.FireOnChange(dv);
                    _variables[name] = dv;
                    OnSet?.Invoke(name, prevValue, dv.Peek());
                    return dv;
                }
                else if (prev == null)
                {
                    dv.FireOnCreate();
                    _variables[name] = dv;
                    OnCreate?.Invoke(name, dv.Peek());
                    return dv;
                }

                _variables[name] = dv;
                return dv;
            }

            if (frame != null)
            {
                // If the binding already exists in *this* overlay, rebind it — mint a
                // new Data and carry subscribers across, never mutate in place. This is
                // the branch that bites inside channel-fire / parallel-foreach: a `set`
                // in a forked flow mutating its overlay Data in place would rewrite a
                // value the parent already stored. Rebinding keeps the captured value
                // independent.
                if (frame.ContainsLocal(name) && frame.TryGet(name, out var existingFrame))
                {
                    var rebound = new data.@this(name, value, type) { Context = _context };
                    rebound.OnCreate = existingFrame.OnCreate;
                    rebound.OnChange = existingFrame.OnChange;
                    rebound.OnDelete = existingFrame.OnDelete;
                    var prevValue = existingFrame.Peek();
                    existingFrame.FireOnChange(rebound);
                    frame.Set(name, rebound);
                    OnSet?.Invoke(name, prevValue, rebound.Peek());
                    return rebound;
                }

                // Either nothing visible, or only visible via Caller chain — mint a
                // fresh local entry that shadows. Mutating an inherited Data would
                // bleed the write up to the caller's scope.
                var data = new data.@this(name, value, type);
                data.Context = _context;
                if (frame.TryGet(name, out var inherited))
                {
                    OnSet?.Invoke(name, inherited.Peek(), value);
                }
                else
                {
                    data.FireOnCreate();
                    OnCreate?.Invoke(name, value);
                }
                frame.Set(name, data);
                return data;
            }

            if (_variables.TryGetValue(name, out var existing))
            {
                // Rebind, don't mutate: mint a new Data and carry the name's event
                // subscribers across (mirrors the Data-value branch above). In-place
                // mutation of `existing` is the alias bug — a Data the variable shared
                // elsewhere (e.g. stored in a list by `add`) gets rewritten underfoot
                // when the variable is re-set. Reassignment rebinds the binding; it
                // does not reach back into a value already captured elsewhere.
                var rebound = new data.@this(name, value, type) { Context = _context };
                rebound.OnCreate = existing.OnCreate;
                rebound.OnChange = existing.OnChange;
                rebound.OnDelete = existing.OnDelete;
                var prevValue = existing.Peek();
                existing.FireOnChange(rebound);
                _variables[name] = rebound;
                OnSet?.Invoke(name, prevValue, rebound.Peek());
                return rebound;
            }
            else
            {
                var data = new data.@this(name, value, type);
                data.Context = _context;
                data.FireOnCreate();
                _variables[name] = data;
                OnCreate?.Invoke(name, value);
                return data;
            }
        }

        // Dot/bracket path: navigate to the parent object, set the property with raw value
        if (!_variables.TryGetValue(rootName, out var root))
        {
            // Root doesn't exist — create it as a dictionary so dot-path properties work
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            root = new data.@this(rootName, dict);
            root.Context = _context;
            _variables[rootName] = root;
        }

        var remaining = name.Length > rootName.Length && name[rootName.Length] == '.'
            ? name[(rootName.Length + 1)..]
            : name[rootName.Length..];

        // Split remaining into parent path + final property name
        var lastDot = remaining.LastIndexOf('.');
        data.@this parent;
        string propertyName;

        if (lastDot >= 0)
        {
            parent = await root.GetChild(remaining[..lastDot]);
            propertyName = remaining[(lastDot + 1)..];
        }
        else
        {
            parent = root;
            propertyName = remaining;
        }

        if (!parent.IsInitialized && parent.Peek() == null)
        {
            // A parse failure on a raw-backed parent stamps MaterializeFailed —
            // surface it rather than masking the real cause with NotFound.
            if (parent.Error?.Key == "MaterializeFailed")
                return data.@this.FromError(parent.Error);
            return data.@this.NotFound(name);
        }

        // Lazy materialize if parent is a typed string (e.g., json) — must happen before navigation
        parent.ForceMaterialize();

        // For dot-path, extract the raw value from Data — we're setting a property on
        // a C# object. No defensive deep-clone here: independence comes from rebinding.
        // Reading a container variable (`%source%`) resolves through WalkContainerVars,
        // which builds a fresh dict/list, so the captured value is already independent
        // of the source's live container — a later `set %source.x% = ...` cannot reach
        // it. The old SnapshotClone deep-copy was a redundant defense (and only ever
        // fired for raw IDictionary/IList, never the native dict).
        var rawValue = value is data.@this dv2 ? dv2.Peek() : value;
        var target = parent.Peek();
        if (target == null)
        {
            if (parent.Error?.Key == "MaterializeFailed")
                return data.@this.FromError(parent.Error);
            return data.@this.NotFound(name);
        }
        var result = SetValueOnObject(target, propertyName, rawValue);
        if (!ReferenceEquals(result, target))
            parent.SetValue(result);
        return root;
    }

    /// <summary>
    /// Sets a property on a target object. If the target is a dictionary, sets the key.
    /// If CLR object with writable property, sets via reflection.
    /// Otherwise converts to a case-insensitive dictionary and sets there.
    /// Returns the (possibly replaced) target.
    /// </summary>
    private static object SetValueOnObject(object target, string propertyName, object? value)
    {
        // Snapshot — editing a captured variable routes to the snapshot's own
        // SetVariable (the owner), so `set %snap.variables.x% = 2` lands on the
        // list Restore reads. Behavior on the owner, not reached-into here.
        if (target is global::app.snapshot.@this snap)
        {
            snap.SetVariable(propertyName, value);
            return target;
        }

        // Native dict — set the key directly. Without this the dict matches no
        // arm below and falls into ConvertToDictionary, which reflects its C#
        // surface (Context/Count/Keys/Entries) into a junk dictionary — losing
        // every real key AND dragging Context (→ App → Culture) into the value,
        // which then cycles when the value is snapshot-cloned.
        if (target is app.type.dict.@this nativeDict)
        {
            nativeDict.Set(propertyName, value);
            return target;
        }

        // Dictionary — set key directly (case-insensitive lookup)
        if (target is IDictionary<string, object?> dict)
        {
            var key = dict.Keys.FirstOrDefault(k =>
                string.Equals(k, propertyName, StringComparison.OrdinalIgnoreCase)) ?? propertyName;
            dict[key] = value;
            return target;
        }

        // Generic IDictionary<string, T> for arbitrary T (JsonObject is the load-bearing
        // case: T = JsonNode?). Without this arm, `set %trace.pass1% = %x%` on a
        // type=json `%trace%` falls through to ConvertToDictionary which replaces the
        // JsonObject with a CLR-reflection dict {Count, Options, Parent, Root} — losing
        // every JSON key. Use reflection on the runtime indexer to write the value;
        // for JsonNode-typed slots, serialize CLR objects through JsonSerializer (which
        // honors [JsonIgnore] etc., so cyclic types like Goal↔Step↔Action don't recurse).
        var stringDictIface = GetStringKeyedDictInterface(target);
        if (stringDictIface != null)
        {
            var valueType = stringDictIface.GetGenericArguments()[1];
            value = ConvertForDictSlot(value, valueType);
            var indexer = stringDictIface.GetProperty("Item");
            if (indexer != null)
            {
                indexer.SetValue(target, value, new object?[] { propertyName });
                return target;
            }
        }

        // Handle bracket indexing: "Steps[0]" → property "Steps", index 0
        var bracketIdx = propertyName.IndexOf('[');
        if (bracketIdx > 0)
        {
            var baseProp = propertyName[..bracketIdx];
            var indexStr = propertyName[(bracketIdx + 1)..].TrimEnd(']');
            var prop = target.GetType().GetProperty(baseProp,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (prop != null)
            {
                var collection = prop.GetValue(target);
                if (collection is System.Collections.IList list && int.TryParse(indexStr, out var idx) && idx >= 0 && idx < list.Count)
                {
                    if (value != null)
                    {
                        var elementType = list.GetType().IsGenericType
                            ? list.GetType().GetGenericArguments()[0]
                            : typeof(object);
                        if (!elementType.IsAssignableFrom(value.GetType()))
                        {
                            var (typedValue, _) = type.catalog.@this.TryConvert(value, elementType);
                            if (typedValue != null) value = typedValue;
                        }
                    }
                    list[idx] = value;
                    return target;
                }
                // Generic IList<T> (e.g., Steps.@this, Actions.@this) — use indexer via reflection
                else if (collection != null && int.TryParse(indexStr, out var gIdx) && gIdx >= 0)
                {
                    var indexer = collection.GetType().GetProperty("Item");
                    var countProp = collection.GetType().GetProperty("Count");
                    if (indexer != null && countProp != null)
                    {
                        var count = (int)countProp.GetValue(collection)!;
                        if (gIdx < count)
                        {
                            if (value != null && !indexer.PropertyType.IsAssignableFrom(value.GetType()))
                            {
                                var (typedValue, _) = type.catalog.@this.TryConvert(value, indexer.PropertyType);
                                if (typedValue != null) value = typedValue;
                            }
                            indexer.SetValue(collection, value, new object[] { gIdx });
                            return target;
                        }
                    }
                }
            }
        }

        // CLR object — try writable property first
        var clrProp = target.GetType().GetProperty(propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (clrProp != null && clrProp.CanWrite)
        {
            if (value != null && !clrProp.PropertyType.IsAssignableFrom(value.GetType()))
            {
                var (typedValue, _) = type.catalog.@this.TryConvert(value, clrProp.PropertyType);
                if (typedValue != null) value = typedValue;
            }
            clrProp.SetValue(target, value);
            return target;
        }

        // Property is read-only or doesn't exist — convert to dictionary
        var converted = ConvertToDictionary(target);
        var dictKey = converted.Keys.FirstOrDefault(k =>
            string.Equals(k, propertyName, StringComparison.OrdinalIgnoreCase)) ?? propertyName;
        converted[dictKey] = value;
        return converted;
    }

    private static Dictionary<string, object?> ConvertToDictionary(object obj)
    {

        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var props = obj.GetType().GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        foreach (var prop in props)
        {
            if (prop.GetIndexParameters().Length > 0) continue; // skip indexers
            dict[prop.Name] = prop.GetValue(obj);
        }
        // Primitive/value type with no navigable properties — preserve original value
        if (dict.Count == 0)
            dict["value"] = obj;
        return dict;
    }

    /// <summary>
    /// Returns the implemented <c>IDictionary&lt;string, T&gt;</c> interface type if
    /// <paramref name="target"/> has one (for any <c>T</c>), else null. Used so the
    /// dot-path set can write through the runtime indexer of foreign string-keyed
    /// dictionaries (notably <see cref="System.Text.Json.Nodes.JsonObject"/>) without
    /// falling through to <see cref="ConvertToDictionary"/> — which would replace the
    /// live JsonObject with a CLR-reflection snapshot and discard its content.
    /// </summary>
    private static System.Type? GetStringKeyedDictInterface(object target)
    {
        foreach (var iface in target.GetType().GetInterfaces())
        {
            if (!iface.IsGenericType) continue;
            if (iface.GetGenericTypeDefinition() != typeof(IDictionary<,>)) continue;
            if (iface.GetGenericArguments()[0] == typeof(string)) return iface;
        }
        return null;
    }

    /// <summary>
    /// Coerces <paramref name="value"/> to fit a dictionary slot typed as <paramref name="slotType"/>.
    /// Already-assignable values pass through. <see cref="System.Text.Json.Nodes.JsonNode"/> slots
    /// (the JsonObject case) get a SerializeToNode round-trip — that's what the rest of the JSON
    /// pipeline expects in the value position, and it honors <c>[JsonIgnore]</c> so cyclic runtime
    /// types like Goal↔Step↔Action don't deadlock the serializer. Other slot types fall back to
    /// TypeMapping; if conversion fails, return the original value and let the caller's indexer
    /// raise the precise error.
    /// </summary>
    private static object? ConvertForDictSlot(object? value, System.Type slotType)
    {
        if (value == null) return null;
        if (slotType.IsAssignableFrom(value.GetType())) return value;

        if (typeof(System.Text.Json.Nodes.JsonNode).IsAssignableFrom(slotType))
        {
            try
            {
                return System.Text.Json.JsonSerializer.SerializeToNode(value, value.GetType(), _snapshotClone);
            }
            catch (System.Exception ex) when (ex is System.Text.Json.JsonException || ex is NotSupportedException)
            {
                return value;
            }
        }

        var (typedValue, _) = type.catalog.@this.TryConvert(value, slotType);
        return typedValue ?? value;
    }

    /// <summary>
    /// Gets a variable by name (supports dot notation path).
    /// </summary>
    // --- Stage 3 accessor surface ---

    /// <summary>Index by name. Returns the Data (NotFound shape when absent — Get is the canonical method).</summary>
    // indexer removed — Get is async (ValueTask); use `await Get(name)`.

    /// <summary>
    /// Diagnostic sync lookup — the in-memory Data for a root name, no async navigation,
    /// no GetChild, no navigable resolution. For `--debug` displays that must run on a
    /// sync surface (event handlers, formatters). Returns null when absent. Content reads
    /// still go through the async <see cref="Get"/> door; this is the in-memory rung only.
    /// </summary>
    public data.@this? Peek(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        name = CleanName(name);
        var rootName = GetRootName(name);
        if (Calls.Current is { } frame && frame.TryGet(rootName, out var framed)) return framed;
        return _variables.TryGetValue(rootName, out var v) ? v : null;
    }

    public async System.Threading.Tasks.ValueTask<data.@this> Get(string name)
    {
        if (string.IsNullOrEmpty(name))
            return data.@this.NotFound(name ?? "");

        name = CleanName(name);

        // Resolve variable references inside bracket indices: [idx] → [1]
        if (name.Contains('['))
            name = ResolveVariablesInPath(name);

        // Handle paths like "user.name" or "items[0].value"
        var rootName = GetRootName(name);
        string? remaining;
        if (name.Length > rootName.Length)
        {
            var sep = name[rootName.Length];
            // Strip leading . (but keep ! as it's the infrastructure marker for GetChild)
            remaining = sep == '.'
                ? name[(rootName.Length + 1)..]
                : name[rootName.Length..];
        }
        else
        {
            remaining = null;
        }

        // Per-call parameter scope wins over actor-shared variables — see Calls.@this.
        data.@this? root;
        if (Calls.Current is { } frame && frame.TryGet(rootName, out var framed))
        {
            root = framed;
        }
        else if (!_variables.TryGetValue(rootName, out root))
        {
            if (_navigables.TryGetValue(rootName, out var resolver))
                return await resolver(remaining ?? "");
            return data.@this.NotFound(name);
        }

        if (string.IsNullOrEmpty(remaining))
            return root;

        var child = await root.GetChild(remaining);
        return child;
    }

    /// <summary>
    /// Gets a typed value by name.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<T?> Get<T>(string name)
    {
        var ov = await Get(name);
        return ov != null ? ov.GetValue<T>() : default;
    }

    /// <summary>
    /// Gets a value by name, returning the raw value or null. A scalar rides as
    /// its wrapper (text/number/bool/…); this accessor is the "raw value" leaf, so
    /// it unwraps the scalar to its backing CLR (text→string, number→numeric,
    /// datetime→DateTimeOffset) — matching what C# callers expect (`as string`,
    /// `(DateTimeOffset)…`). Collections stay native (dict/list keep their type);
    /// callers that want the wrapper use <c>Get(name).Value</c>.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<object?> GetValue(string name)
    {
        var ov = await Get(name);
        var v = ov?.Materialize();
        if (v is app.type.dict.@this or app.type.list.@this) return v;
        return v is app.type.item.@this iv ? iv.ToRaw() : v;
    }

    /// <summary>
    /// Checks if a variable exists.
    /// </summary>
    public bool Contains(string name)
    {
        name = CleanName(name);
        var rootName = GetRootName(name);
        if (Calls.Current is { } frame && frame.TryGet(rootName, out _))
            return true;
        return _variables.ContainsKey(rootName);
    }

    /// <summary>
    /// Removes a variable. Fires OnDelete on the removed Data so subscribers
    /// (e.g. --debug={"variables":[{"name":"x","event":"OnDelete"}]}) see it.
    /// </summary>
    public bool Remove(string name)
    {
        name = CleanName(name);
        if (_variables.TryRemove(name, out var removed))
        {
            removed.FireOnDelete();
            OnRemove?.Invoke(name);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Returns variables that changed since the given time. Uses Data.Updated timestamp.
    /// </summary>
    public Dictionary<string, string> GetChangedSince(DateTime since)
    {
        var result = new Dictionary<string, string>();
        foreach (var (name, data) in _variables)
        {
            if (name.StartsWith("!")) continue; // skip system variables
            if (data.Updated > since)
                result[name] = data.Materialize()?.ToString() ?? "(null)";
        }
        return result;
    }

    /// <summary>
    /// Resolves %variable% references in a string using this Variables instance.
    /// Returns the input unchanged if no %var% patterns are found.
    /// When <paramref name="skipInfrastructure"/> is true, %!variable% references (infrastructure
    /// variables like %!app%, %!callStack%) are left unresolved. Use this for untrusted input
    /// (e.g., file content, HTTP responses) to prevent information disclosure.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<string> Resolve(string input, bool skipInfrastructure = false)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains('%'))
            return input;

        // The async door can't be awaited inside Regex.Replace's sync MatchEvaluator
        // (the sync wall — no GetAwaiter().GetResult()). So pre-resolve every distinct
        // %var% through the door first, then the sync replace just looks up.
        // Scalar/output access (access-driven resolution): a bare `%x%` renders the
        // value's raw source form via Peek(), not a structured parse — `%cfg%` of a
        // lazily-read config.json is the raw json string. Dotted paths navigate via
        // Get and come back already materialized.
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(input, @"%([^%]+)%"))
        {
            var varName = m.Groups[1].Value;
            if (skipInfrastructure && varName.StartsWith('!')) continue;
            if (resolved.ContainsKey(varName)) continue;
            var s = (await Get(varName))?.Peek()?.ToString();
            if (s != null) resolved[varName] = s;
        }

        return Regex.Replace(input, @"%([^%]+)%", match =>
        {
            var varName = match.Groups[1].Value;
            if (skipInfrastructure && varName.StartsWith('!'))
                return match.Value; // Leave %!var% unresolved for untrusted input
            return resolved.TryGetValue(varName, out var v) ? v : match.Value;
        });
    }

    /// <summary>
    /// Gets all variable names.
    /// </summary>
    public IEnumerable<string> GetNames()
    {
        return _variables.Keys.Where(k => !k.StartsWith("!"));
    }

    /// <summary>
    /// Gets all variables ordered by last update.
    /// </summary>
    public IEnumerable<KeyValuePair<string, data.@this>> GetAll()
    {
        return _variables
            .Where(kvp => !kvp.Key.StartsWith("!"))
            .OrderByDescending(kvp => kvp.Value.Updated);
    }

    /// <summary>
    /// Clears all non-system variables.
    /// </summary>
    public void Clear()
    {
        var toRemove = _variables
            .Where(kvp => !kvp.Key.StartsWith("!") && kvp.Value is not data.DynamicData)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            _variables.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Creates a deep clone of this Variables instance.
    /// Values are deep-cloned so mutations in the clone do not affect the original.
    /// </summary>
    public @this Clone()
    {
        var clone = new @this();
        foreach (var kvp in _variables)
        {
            // Data.DynamicData (Now, GUID, etc.) — already in clone from constructor
            if (kvp.Value is data.DynamicData) continue;

            // System context vars (! prefix) — skip, they're per-execution
            if (kvp.Key.StartsWith("!")) continue;

            clone._variables[kvp.Key] = kvp.Value.Clone();
        }
        // Share navigable registrations by reference so the cloned Variables
        // resolves %Settings.X% identically. The resolvers are stateless (they
        // close over an actor + app); cloning them would be meaningless.
        foreach (var nav in _navigables) clone._navigables[nav.Key] = nav.Value;
        clone.Context = Context;
        return clone;
    }

    /// <summary>
    /// Saves a snapshot of current variable keys for later restore.
    /// </summary>
    public HashSet<string> Save() => new HashSet<string>(_variables.Keys, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Restores to a saved snapshot: removes any variables added after the snapshot.
    /// </summary>
    public void Restore(HashSet<string> snapshot)
    {
        foreach (var key in _variables.Keys)
        {
            if (!snapshot.Contains(key))
                _variables.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Converts Variables to a dictionary (for serialization/debugging).
    /// </summary>
    public Dictionary<string, object?> ToDictionary(bool includeSystem = false)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _variables)
        {
            if (!includeSystem && kvp.Key.StartsWith("!"))
                continue;
            dict[kvp.Key] = kvp.Value.Peek();
        }
        return dict;
    }

    /// <summary>
    /// Captures user-visible variables for failure diagnostics (e.g. assertion errors).
    /// Excludes:
    ///  - infrastructure vars (!-prefixed, e.g. !app, !fileSystem)
    ///  - dynamic system vars (Now, NowUtc, GUID) — always-fresh, no diagnostic value
    /// (Settings is a navigable resolver, not a Data subclass — never appears in _variables.)
    /// Values are captured by reference (architect §5.7). Called from assert handlers
    /// on failure only; the App is about to be disposed, so by-ref is safe.
    /// ConcurrentDictionary enumeration is snapshot-style and safe during concurrent writes.
    /// </summary>
    public Dictionary<string, object?> Snapshot()
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _variables)
        {
            if (kvp.Key.StartsWith("!")) continue;
            if (kvp.Value is data.DynamicData) continue;
            dict[kvp.Key] = kvp.Value.Peek();
        }
        return dict;
    }

    private static readonly AsyncLocal<HashSet<string>?> _resolvingVars = new();

    /// <summary>
    /// Resolves variable names inside bracket indices.
    /// e.g. "addresses[idx].street" with idx=1 → "addresses[1].street"
    /// Uses an async-local visited set to detect circular references (a→b→a) — survives
    /// future await introductions; ThreadStatic would race under Task.WhenAll.
    /// </summary>
    private string ResolveVariablesInPath(string path)
    {
        var resolving = _resolvingVars.Value;
        var isRoot = resolving == null;
        if (isRoot)
        {
            resolving = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _resolvingVars.Value = resolving;
        }
        try
        {
            return Regex.Replace(path, @"\[([^\]\d][^\]]*)\]", match =>
            {
                var varName = match.Groups[1].Value;
                if (!resolving!.Add(varName))
                    return match.Value; // Circular reference — leave unresolved
                try
                {
                    var resolved = GetValue(varName);
                    return resolved != null ? $"[{resolved}]" : match.Value;
                }
                finally
                {
                    resolving.Remove(varName);
                }
            });
        }
        finally
        {
            if (isRoot) _resolvingVars.Value = null;
        }
    }

    private static string CleanName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        return name.Trim().TrimStart('%').TrimEnd('%');
    }

    private static string GetRootName(string path)
    {
        var dotIndex = path.IndexOf('.');
        var bracketIndex = path.IndexOf('[');
        var bangIndex = path.IndexOf('!');
        // ! at position 0 is part of the variable name (!app), not a separator
        if (bangIndex == 0) bangIndex = path.IndexOf('!', 1);

        // Find the earliest separator
        int min = int.MaxValue;
        if (dotIndex >= 0) min = Math.Min(min, dotIndex);
        if (bracketIndex >= 0) min = Math.Min(min, bracketIndex);
        if (bangIndex > 0) min = Math.Min(min, bangIndex);

        return min == int.MaxValue ? path : path[..min];
    }
}

/// <summary>
/// Provides async-local access to Variables.
/// </summary>
public interface IVariablesAccessor
{
    @this Current { get; set; }
}

/// <summary>
/// Default implementation using AsyncLocal.
/// </summary>
public class @thisAccessor : IVariablesAccessor
{
    private static readonly AsyncLocal<@this> _current = new();

    public @this Current
    {
        get => _current.Value ?? (_current.Value = new @this());
        set => _current.Value = value;
    }
}
