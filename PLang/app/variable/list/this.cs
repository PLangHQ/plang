using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Force.DeepCloner;
using app.actor.context;

namespace app.variable.list;

/// <summary>
/// Thread-safe variable storage for App. This is the STORE (a collection), not a value
/// type — the "variable" type name belongs to variable.@this (the raw-name value); this
/// class must not claim it, or App.Type["variable"] shadows the name-object.
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
    private actor.context.@this _context;

    /// <summary>
    /// Registers a navigable mount: when <see cref="Get"/> resolves <c>name.X</c>
    /// and <c>name</c> isn't found in the regular variable scope, the resolver
    /// is called with the full path remainder (e.g. <c>"X"</c> or <c>"X.Y"</c>)
    /// and its result is returned. Used by Settings: each actor's Variables
    /// registers <c>"setting"</c> with a resolver that delegates to
    /// <c>app.Setting.Get(path, this.Context)</c>. Generalises to any future
    /// non-Data navigable mount.
    /// </summary>
    public void RegisterNavigable(string name, Func<string, System.Threading.Tasks.ValueTask<data.@this>> resolver)
        => _navigables[name] = resolver;


    /// <summary>
    /// Per-call parameter scopes. <see cref="Get"/> consults <c>Calls.Current</c> before
    /// falling back to the actor-shared dictionary — that's how goal-call parameters
    /// (e.g. <c>%!data%</c> on a goal channel) avoid racing across concurrent calls on
    /// the same actor.
    /// </summary>
    [JsonIgnore]
    public call.list.@this Calls { get; } = new();

    [JsonIgnore]
    internal actor.context.@this Context
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

    /// <summary>
    /// Production ctor — born from the owning context. Every Variables in a running
    /// App belongs to exactly one context, passed in here.
    /// </summary>
    public @this(actor.context.@this context)
    {
        Context = context;
        // System variables are born WITH context — a computed lifts its factory result
        // through the registry, so it must hold a context at birth (not stamped after).
        _variables["Now"] = new data.DynamicData("Now", () => DateTimeOffset.Now, context, app.type.@this.DateTime);
        _variables["NowUtc"] = new data.DynamicData("NowUtc", () => DateTimeOffset.UtcNow, context, app.type.@this.DateTime);
        _variables["GUID"] = new data.DynamicData("GUID", () => Guid.NewGuid(), context, context.Type.Create("guid"));
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
    public async System.Threading.Tasks.ValueTask<data.@this> Set(string name, object? value)
    {
        // A reference value (%x%) binds the referenced VALUE, not the reference marker. The
        // instance Gets itself (lazy name-hop: the target's value door is never opened here — no
        // eager read), and `name` gets a ShallowClone of it — the documented `set %y% = %x%` rule:
        // the value INSTANCE is shared (immutable, so safe) so it stays lazy, while the Properties
        // bag is COPIED so a later `%y%!prop` write never bleeds onto x. Copy semantics: y captures
        // x's CURRENT value, not its future reassignments. Storing the marker verbatim would go
        // stale (!data rebinds every action) and a self-assign (`set %a% = %a%`) would cycle on the
        // value door; the shallow-clone avoids both. Each reference carrier resolves its own name
        // (variable/source/text) — the courier just asks. A miss flows through as-is.
        if (value is data.@this reference && reference.IsVariable)
        {
            var bound = await reference.Get(_context);
            value = bound is { IsInitialized: true } ? bound.ShallowClone(name) : bound;
        }

        // Names arrive clean — the builder normalizes them before the .pr, and runtime C# callers
        // construct clean names; the store does not re-process at runtime.

        // The path owns tokenization + per-hop index resolution (no regex pre-pass). A bare root
        // (no dot/bracket) is a direct rebind of the variable; a deep path is write-at-path on the
        // root's own value — the READ walk to the parent, then one Set at the leaf (data.Set).
        var path = global::app.variable.path.@this.Parse(name);

        // Simple case: no dot/bracket path — set the root variable directly
        if (path.Tail.IsEmpty)
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
                    var rebound = new data.@this(name, value, context: _context);
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
                var data = new data.@this(name, value, context: _context);
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
                var rebound = new data.@this(name, value, context: _context);
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
                var data = new data.@this(name, value, context: _context);
                data.FireOnCreate();
                _variables[name] = data;
                OnCreate?.Invoke(name, value);
                return data;
            }
        }

        // Deep path: write-at-path on the root's own value. Create the root as a native dict when
        // absent (so `set %x.a% = 1` on a fresh %x% works), then hand data.Set the tail — the read
        // walk to the leaf's parent + one Set door. The value owns its own child write; there is no
        // reflection fallback and no dict-conversion — an unsettable target throws, loud.
        if (!_variables.TryGetValue(path.Root, out var root))
        {
            root = new data.@this(path.Root, new global::app.type.dict.@this(_context), context: _context);
            _variables[path.Root] = root;
        }

        return await root.Set(path.Tail, value);
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
        var rootName = global::app.variable.path.@this.Parse(name).Root;
        if (Calls.Current is { } frame && frame.TryGet(rootName, out var framed)) return framed;
        return _variables.TryGetValue(rootName, out var v) ? v : null;
    }

    /// <summary>The value-counterpart to <see cref="Get"/>: hand back the VALUE a
    /// name holds, opened through its own door. The binding carries its own context,
    /// so a reference stored under another scope resolves there (goal-call by-value).</summary>
    public async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(string name)
        => await (await Get(name)).Value();

    public async System.Threading.Tasks.ValueTask<data.@this> Get(string name)
    {
        if (string.IsNullOrEmpty(name))
            return _context.NotFound(name ?? "");

        name = CleanName(name);

        // Bracket indices (`[planStep.index]`) resolve inside the walk now
        // (Segment.Index.Key) — no pre-pass string rewrite.

        // Handle paths like "user.name" or "items[0].value"
        var rootName = global::app.variable.path.@this.Parse(name).Root;
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
            return _context.NotFound(name);
        }

        if (string.IsNullOrEmpty(remaining))
            return root;

        var child = await root.Get(remaining);
        return child;
    }

    /// <summary>
    /// Typed ask on the variable store — returns <c>Data&lt;T&gt;</c>. Identity hop:
    /// if the variable already holds a <typeparamref name="T"/>, its OWN Data is
    /// returned (aliasing/shared-sample/narrowing preserved). Otherwise the value
    /// is converted via <c>T.Create</c> into a NEW <c>Data&lt;T&gt;</c> — the stored
    /// variable is never rebound. Absent → Uninitialized; a decline carries its
    /// error. The typed door the parameter binder and goal-call mapping ask through.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<data.@this<T>> Get<T>(string name)
        where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        var existing = await Get(name);
        // Get never returns null and hands back a context-ful NotFound on a miss — return
        // its typed view (a value-less Data<T> with context intact), don't synthesize a
        // context-less Uninitialized.
        if (!existing.IsInitialized)
            return existing.As<T>();
        if (existing is data.@this<T> already) return already;          // identity hop
        var item = await existing.Value<T>();                          // T.Create(await Value(), existing)
        if (item == null) return data.@this<T>.From(existing);         // decline carries the error
        var typed = _context.Ok<T>(item);
        typed.Context = _context;
        return typed;
    }

    /// <summary>
    /// Checks if a variable exists.
    /// </summary>
    public bool Contains(string name)
    {
        name = CleanName(name);
        var rootName = global::app.variable.path.@this.Parse(name).Root;
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
                result[name] = data.Peek()?.ToString() ?? "(null)";
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
            var dataVar = await Get(varName);
            // A reference to an unset USER variable is an error at the reference site —
            // strict, like any typed language, and consistent with the full-match door
            // (variable.Value / text.Value both throw/Fail on an absent ref). Infrastructure
            // refs (%!x%) are optionally-present engine internals (%!error% with no error,
            // etc.) — they stay literal on absence, never error, whether or not
            // skipInfrastructure is set.
            if (dataVar == null || !dataVar.IsInitialized)
            {
                if (varName.StartsWith('!')) continue;   // infra ref absent → leave literal
                throw new global::app.error.VariableNotFoundException(varName);
            }
            string? s;
            if (dataVar?.Peek() is global::app.type.file.@this or global::app.type.url.@this)
            {
                // Interpolation is SCALAR use — a reference renders its content
                // (the bare-scalar contract), never the location string.
                var content = await dataVar.Value();
                s = content is global::app.type.binary.@this bin
                    ? System.Convert.ToBase64String(bin.Value) : content?.ToString();
            }
            // A stored variable reference resolves to its value (variable.Value() =>
            // value.Value()) — so %y% set from %x% renders x's value, not the ref name.
            // Everything else renders its raw current form (a source stays raw json).
            else if (dataVar?.Peek() is global::app.variable.@this)
                s = (await dataVar.Value())?.ToString();
            else s = dataVar?.Peek()?.ToString();
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
        var clone = new @this(_context);
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

    private static string CleanName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        return name.Trim().TrimStart('%').TrimEnd('%');
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
        // No lazy-create: a Variables store is always Set on the async flow before it is
        // read (born from its owning context). A null here is a caller reading before set —
        // surface it, never paper it over with a context-less store.
        get => _current.Value!;
        set => _current.Value = value;
    }
}
