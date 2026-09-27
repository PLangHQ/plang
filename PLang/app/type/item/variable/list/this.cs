using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Force.DeepCloner;
using app.actor.context;

namespace app.type.item.variable.list;

/// <summary>
/// Thread-safe variable storage for App. This is the STORE (a collection), not a value
/// type — the "variable" type name belongs to variable.@this (the raw-name value); this
/// class must not claim it, or App.Type["variable"] shadows the name-object.
/// </summary>
public partial class @this
{
    private readonly ConcurrentDictionary<string, data.@this> _variables = new(StringComparer.OrdinalIgnoreCase);
    private actor.context.@this _context;

    /// <summary>
    /// Per-call parameter scopes. <see cref="Get"/> consults <c>Calls.Current</c> before
    /// falling back to the actor-shared dictionary — that's how goal-call parameters
    /// (e.g. <c>%!data%</c> on a goal channel) avoid racing across concurrent calls on
    /// the same actor.
    /// </summary>
    [JsonIgnore]
    public call.list.@this Calls { get; } = new();

    /// <summary>True when the current flow's frame was pushed for <paramref name="call"/> and its
    /// runner supplied <paramref name="name"/> — the supplied value wins over the call's own row.</summary>
    public bool Supplies(global::app.goal.step.action.@this call, string name)
        => Calls.Current?.Supplies(call, name) ?? false;

    [JsonIgnore]
    internal actor.context.@this Context
    {
        get => _context;
        set => _context = value;
    }

    /// <summary>
    /// Fires after a variable is rebound (existing name → new value). Carries (name, before, after).
    /// Collection-level event — fires for any name.
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
        // Built with the actors, before the app's types exist: each type carries its class itself.
        var datetime = new app.type.@this("datetime", typeof(app.type.item.datetime.@this));
        _variables["Now"] = new data.DynamicData("Now", () => DateTimeOffset.Now, context, datetime);
        _variables["NowUtc"] = new data.DynamicData("NowUtc", () => DateTimeOffset.UtcNow, context, datetime);
        _variables["GUID"] = new data.DynamicData("GUID", () => Guid.NewGuid(), context,
            new app.type.@this("guid", typeof(app.type.item.guid.@this)));
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
    /// <paramref name="name"/> is a variable's root; a write deeper in is the variable's own.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<data.@this> Set(string name, object? value)
    {
        // A reference value (%x%) binds the referenced VALUE, not the reference marker. The
        // instance Gets itself (lazy name-hop: the target's value door is never opened here — no
        // eager read), and `name` gets a Copy of it — the documented `set %y% = %x%` rule:
        // the value INSTANCE is shared (immutable, so safe) so it stays lazy, while the Properties
        // bag is COPIED so a later `%y%!prop` write never bleeds onto x. Copy semantics: y captures
        // x's CURRENT value, not its future reassignments. Storing the marker verbatim would go
        // stale (!data rebinds every action) and a self-assign (`set %a% = %a%`) would cycle on the
        // value door; the copy avoids both. Each reference carrier resolves its own name
        // (variable/source/text) — the courier just asks. A miss flows through as-is. The
        // reference resolves with the context of the Data that carries it (a goal-call argument
        // `place=%city%` reads the CALLER's memory, whichever store it lands in).
        if (value is data.@this reference && reference.IsVariable)
        {
            var bound = await reference.Get(reference.Context);
            value = bound is { IsInitialized: true } ? bound.Copy(name) : bound;
        }

        // The name is a variable's root; a write deeper in is the variable's own (variable.Set).

        // If a Calls overlay is active (we're inside a forked flow — channel fire,
        // parallel foreach iteration, etc.), route the write into the overlay so
        // siblings can't see it. Reads cascade overlay → caller chain → underlying
        // dict, so subsequent gets here see the new value.
        var frame = Calls.Current;

        // Data value: replace under `name`; the store announces the create or change (OnCreate /
        // OnSet — the diff capture and the --debug watch listen there). In-place mutation of prev
        // is wrong: a Data may be aliased under multiple keys (e.g. Action stores the step result
        // both under its own name AND under "!data"), so mutating prev would bleed across keys.
        // Properties stay attached to the Data instance — they're result metadata (e.g.
        // condition.if's branchIndex), not binding metadata. A stored Data keeps the context it
        // was born with.
        if (value is data.@this dv)
        {
            if (frame != null)
            {
                var hadPrev = frame.TryGet(name, out var prevFrame);
                if (hadPrev && !ReferenceEquals(prevFrame, dv))
                {
                    var prevValue = prevFrame.Peek();
                    frame.Set(name, dv);
                    OnSet?.Invoke(name, prevValue, dv.Peek());
                    return dv;
                }
                else if (!hadPrev)
                {
                    frame.Set(name, dv);
                    OnCreate?.Invoke(name, dv.Peek());
                    return dv;
                }
                frame.Set(name, dv);
                return dv;
            }

            if (_variables.TryGetValue(name, out var prev) && !ReferenceEquals(prev, dv))
            {
                var prevValue = prev.Peek();
                _variables[name] = dv;
                OnSet?.Invoke(name, prevValue, dv.Peek());
                return dv;
            }
            else if (prev == null)
            {
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
            // new Data, never mutate in place. This is the branch that bites inside
            // channel-fire / parallel-foreach: a `set` in a forked flow mutating its
            // overlay Data in place would rewrite a value the parent already stored.
            // Rebinding keeps the captured value independent.
            if (frame.ContainsLocal(name) && frame.TryGet(name, out var existingFrame))
            {
                var rebound = new data.@this(name, value, context: _context);
                var prevValue = existingFrame.Peek();
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
                OnCreate?.Invoke(name, value);
            }
            frame.Set(name, data);
            return data;
        }

        if (_variables.TryGetValue(name, out var existing))
        {
            // Rebind, don't mutate: mint a new Data (mirrors the Data-value branch above).
            // In-place mutation of `existing` is the alias bug — a Data the variable shared
            // elsewhere (e.g. stored in a list by `add`) gets rewritten underfoot when the
            // variable is re-set. Reassignment rebinds the binding; it does not reach back
            // into a value already captured elsewhere.
            var rebound = new data.@this(name, value, context: _context);
            var prevValue = existing.Peek();
            _variables[name] = rebound;
            OnSet?.Invoke(name, prevValue, rebound.Peek());
            return rebound;
        }
        else
        {
            var data = new data.@this(name, value, context: _context);
            _variables[name] = data;
            OnCreate?.Invoke(name, value);
            return data;
        }
    }

    /// <summary>What <paramref name="name"/> holds — or, when it holds nothing, the value
    /// <paramref name="value"/> makes, stored under it in one step: runs asking at once all answer
    /// the same Data, never each a value of their own. A forked flow's own scope keeps
    /// <see cref="Set(string, object?)"/>'s rules.</summary>
    public async System.Threading.Tasks.ValueTask<data.@this> Ensure(string name, System.Func<global::app.type.item.@this> value)
    {
        var existing = await Get(name);
        if (existing.IsInitialized) return existing;
        if (Calls.Current != null)
            return await Set(name, value());

        data.@this? born = null;
        var held = _variables.GetOrAdd(name, _ => born = new data.@this(name, value(), context: _context));
        if (ReferenceEquals(held, born)) OnCreate?.Invoke(name, born.Peek());
        return held;
    }

    /// <summary>Stores <paramref name="value"/> under <paramref name="name"/> only if the name still
    /// holds <paramref name="expected"/> — the Data the caller read — in one step; a newer value set
    /// in between is left alone. Answers whether the name now holds the value. When
    /// <paramref name="expected"/> already holds it there is nothing to write.</summary>
    public async System.Threading.Tasks.ValueTask<bool> Replace(string name, data.@this expected, global::app.type.item.@this value)
    {
        if (ReferenceEquals(expected.Peek(), value)) return true;

        var frame = Calls.Current;
        if (frame != null ? !(frame.TryGet(name, out var held) && ReferenceEquals(held, expected))
                          : !_variables.TryGetValue(name, out held) || !ReferenceEquals(held, expected))
            return false;

        // Rebind — the same rebind Set does.
        var rebound = new data.@this(name, value, context: _context);
        if (frame != null) frame.Set(name, rebound);
        else if (!_variables.TryUpdate(name, rebound, expected)) return false;
        OnSet?.Invoke(name, expected.Peek(), value);
        return true;
    }


    /// <summary>
    /// Diagnostic sync lookup — the in-memory Data a name holds, no async navigation. For
    /// `--debug` displays that must run on a sync surface (event handlers, formatters). Returns
    /// null when absent. Content reads still go through the async <see cref="Get"/> door; this is
    /// the in-memory rung only.
    /// </summary>
    public data.@this? Peek(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (Calls.Current is { } frame && frame.TryGet(name, out var framed)) return framed;
        return _variables.TryGetValue(name, out var v) ? v : null;
    }

    /// <summary>The value-counterpart to <see cref="Get"/>: hand back the VALUE a
    /// name holds, opened through its own door. The binding carries its own context,
    /// so a reference stored under another scope resolves there (goal-call by-value).</summary>
    public async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(string name)
        => await (await Get(name)).Value();

    /// <summary>What <paramref name="name"/> holds — a variable's root; a variable reaches deeper
    /// through its own code. A per-call parameter scope wins over the actor-shared variables
    /// (<see cref="Calls"/>). NotFound when nothing is bound.</summary>
    public System.Threading.Tasks.ValueTask<data.@this> Get(string name)
    {
        if (string.IsNullOrEmpty(name))
            return System.Threading.Tasks.ValueTask.FromResult(_context.NotFound(name ?? ""));
        if (Calls.Current is { } frame && frame.TryGet(name, out var framed))
            return System.Threading.Tasks.ValueTask.FromResult(framed);
        return System.Threading.Tasks.ValueTask.FromResult(
            _variables.TryGetValue(name, out var root) ? root : _context.NotFound(name));
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
        return _context.Ok<T>(item);
    }

    /// <summary>
    /// Checks if a variable exists.
    /// </summary>
    public bool Contains(string name)
    {
        if (Calls.Current is { } frame && frame.TryGet(name, out _))
            return true;
        return _variables.ContainsKey(name);
    }

    /// <summary>
    /// Removes a variable; the store announces it (OnRemove).
    /// </summary>
    public bool Remove(string name)
    {
        if (_variables.TryRemove(name, out _))
        {
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
    /// Each variable rides whole — its Data (name, type, value), held by reference — keyed by its
    /// name, so <c>%!error.Variables.foo%</c> navigates to it. Called when an error happens (assert,
    /// and every recorded error under --debug). ConcurrentDictionary enumeration is snapshot-style
    /// and safe during concurrent writes.
    /// </summary>
    public global::app.type.item.dict.@this Snapshot()
    {
        var vars = new global::app.type.item.dict.@this();
        foreach (var kvp in _variables)
        {
            if (kvp.Key.StartsWith("!")) continue;
            if (kvp.Value is data.DynamicData) continue;
            vars.Set(kvp.Key, kvp.Value);
        }
        return vars;
    }
}
