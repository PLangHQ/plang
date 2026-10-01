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
    /// <summary>How two variable names compare: a name is the same variable whatever its case (<c>%Greeting%</c> is
    /// <c>%greeting%</c>).</summary>
    internal static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    private readonly ConcurrentDictionary<string, data.@this> _variables = new(Comparer);
    private actor.context.@this _context;

    /// <summary>
    /// Per-call parameter scopes. <see cref="Get"/> consults <c>Calls.Current</c> before
    /// falling back to the actor-shared dictionary — that's how goal-call parameters
    /// (e.g. <c>%!data%</c> on a goal channel) avoid racing across concurrent calls on
    /// the same actor.
    /// </summary>
    [JsonIgnore]
    public call.list.@this Calls { get; } = new();

    /// <summary>How deep a chain of references this flow is resolving (a variable holding a variable…) —
    /// the variable's read counts it, and a chain past its limit is a cycle.</summary>
    internal System.Threading.AsyncLocal<int> Resolving { get; } = new();

    /// <summary>The values this flow is writing into templates — one reached again while it is written (a
    /// variable holding a template that names it) is a cycle.</summary>
    internal System.Threading.AsyncLocal<System.Collections.Immutable.ImmutableHashSet<object>?> Rendering { get; } = new();

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
        _variables["Now"] = new data.DynamicData("Now", asker => asker.Ok(DateTimeOffset.Now), context, datetime);
        _variables["NowUtc"] = new data.DynamicData("NowUtc", asker => asker.Ok(DateTimeOffset.UtcNow), context, datetime);
        _variables["GUID"] = new data.DynamicData("GUID", asker => asker.Ok(Guid.NewGuid()), context,
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
        // A reference value (%x%) binds the referenced VALUE as it is now, not the reference marker
        // (data.Settle: the value INSTANCE is shared, so it stays lazy; the Properties bag is its own,
        // so a later `%y%!prop` write never bleeds onto x). y captures x's CURRENT value, not its future
        // reassignments. Storing the marker verbatim would go stale (!data rebinds every action) and a
        // self-assign (`set %a% = %a%`) would cycle on the value door. The reference resolves with the
        // context of the Data that carries it (a goal-call argument `place=%city%` reads the CALLER's
        // memory, whichever store it lands in). A miss flows through as-is; what binds is renamed to
        // `name`.
        if (value is data.@this reference && reference.IsVariable)
        {
            var bound = await reference.Settle();
            value = bound.IsInitialized ? bound.Copy(name) : bound;
        }

        // The name is a variable's root; a write deeper in is the variable's own (variable.Set).

        // What is bound before the set answers first: a refusal or a cancel is the answer, nothing written.
        if (await Before(Events?.set, name, value) is { } refused) return refused;

        var (stored, changed, before) = Bind(name, value);
        return changed ? await After(name, before, stored) : stored;
    }

    // Binds `name` to `value`: the Data stored, whether the name changed (the same Data set again doesn't),
    // and what it held before (null when it was new).
    private (data.@this Stored, bool Changed, object? Before) Bind(string name, object? value)
    {
        // Inside a call, the write lands in the frame that binds the name (a loop's %item%, a call's
        // parameter), or — none does — in the actor's memory, as it would without the call. Reads cascade
        // frame → caller chain → memory, so later gets see the new value.
        var frame = Calls.Current?.Keeper(name);

        // Data value: replace under `name`. In-place mutation of prev
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
                var prevValue = hadPrev ? prevFrame.Peek() : null;
                frame.Set(name, dv);
                return hadPrev && ReferenceEquals(prevFrame, dv) ? (dv, false, null) : (dv, true, prevValue);
            }

            var had = _variables.TryGetValue(name, out var prev);
            var before = had ? prev!.Peek() : null;
            _variables[name] = dv;
            return had && ReferenceEquals(prev, dv) ? (dv, false, null) : (dv, true, before);
        }

        if (frame != null)
        {
            // The binding exists in the keeping frame: rebind it — mint a new Data, never mutate in
            // place, so a value a caller already captured stays independent.
            if (frame.ContainsLocal(name) && frame.TryGet(name, out var existingFrame))
            {
                var rebound = new data.@this(name, value, context: _context);
                var prevValue = existingFrame.Peek();
                frame.Set(name, rebound);
                return (rebound, true, prevValue);
            }

            // Either nothing visible, or only visible via Caller chain — mint a
            // fresh local entry that shadows. Mutating an inherited Data would
            // bleed the write up to the caller's scope.
            var data = new data.@this(name, value, context: _context);
            var inheritedValue = frame.TryGet(name, out var inherited) ? inherited.Peek() : null;
            frame.Set(name, data);
            return (data, true, inheritedValue);
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
            return (rebound, true, prevValue);
        }

        var fresh = new data.@this(name, value, context: _context);
        _variables[name] = fresh;
        return (fresh, true, null);
    }

    // The variable type's events — once the app has it: the actors' stores are born before the app's types.
    private global::app.@event.on.@this? Events => _context.App.variable?.on;

    // What is bound before `name` changes to `value`: null to go on — nothing bound, or nothing refused — else a
    // failure or a Handled answer, which is the write's answer. The variable is made only when something is bound.
    private System.Threading.Tasks.ValueTask<data.@this?> Before(global::app.@event.@this? @event, string name, object? value)
        => @event is { before.Count: > 0 } ? Before(@event, name, value as data.@this ?? new data.@this(name, value, context: _context)) : default;

    private async System.Threading.Tasks.ValueTask<data.@this?> Before(global::app.@event.@this @event, string name, data.@this about)
    {
        var answer = await @event.Before(Named(name), _context, about);
        return answer is { Success: false } or { Handled: true } ? answer : null;
    }

    // `name` changed from `before` (null: it was new) to `stored`: the call stack records it for its history, then
    // what is bound after the set starts on the stored Data — its answer is the set's.
    private System.Threading.Tasks.ValueTask<data.@this> After(string name, object? before, data.@this stored)
    {
        _context.CallStack.Record(this, name, before);
        return Events?.set is { after.Count: > 0 } set ? set.After(Named(name), stored, _context) : new(stored);
    }

    // The variable named `name`, as its events are handed it.
    private global::app.type.item.variable.@this Named(string name) => new parser.@this($"%{name}%").Variable.Single();

    /// <summary>What <paramref name="name"/> holds — or, when it holds nothing, the value
    /// <paramref name="value"/> gives birth to, stored under it in one step: runs asking at once all answer
    /// the same Data, never each a value of their own. A birth that is refused or answered otherwise is the
    /// answer, nothing stored. A name a call's frame keeps follows <see cref="Set(string, object?)"/>'s rules.</summary>
    public async System.Threading.Tasks.ValueTask<data.@this> Ensure(string name,
        System.Func<System.Threading.Tasks.ValueTask<data.@this>> value)
    {
        var existing = await Get(name);
        if (existing.IsInitialized) return existing;
        var born = await value();
        if (!born.Success || born.Handled) return born;
        var made = born.Peek();
        if (Calls.Current?.Keeper(name) != null)
            return await Set(name, made);

        if (await Before(Events?.set, name, made) is { } refused) return refused;
        data.@this? stored = null;
        var held = _variables.GetOrAdd(name, _ => stored = new data.@this(name, made, context: _context));
        return ReferenceEquals(held, stored) ? await After(name, null, held) : held;
    }

    /// <summary>Stores <paramref name="value"/> under <paramref name="name"/> only if the name still
    /// holds <paramref name="expected"/> — the Data the caller read — in one step; a newer value set
    /// in between is left alone, and is the answer. When <paramref name="expected"/> already holds the
    /// value there is nothing to write. What is bound before the set refusing or cancelling it is the answer;
    /// so is what is bound after it.</summary>
    public async System.Threading.Tasks.ValueTask<data.@this> Replace(string name, data.@this expected, global::app.type.item.@this value)
    {
        if (ReferenceEquals(expected.Peek(), value)) return expected;
        if (await Before(Events?.set, name, value) is { } refused) return refused;

        var frame = Calls.Current?.Keeper(name);
        if (frame != null ? !(frame.TryGet(name, out var held) && ReferenceEquals(held, expected))
                          : !_variables.TryGetValue(name, out held) || !ReferenceEquals(held, expected))
            return held ?? expected;

        // Rebind — the same rebind Set does.
        var rebound = new data.@this(name, value, context: _context);
        if (frame != null) frame.Set(name, rebound);
        else if (!_variables.TryUpdate(name, rebound, expected)) return await Get(name);
        return await After(name, expected.Peek(), rebound);
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
    /// Removes a variable, through the variable type's <c>on.remove</c>: what is bound before it is handed the
    /// value the name holds, and a refusal or a cancel is the answer, the variable left; what is bound after it
    /// is handed the value removed. Answers the value removed, NotFound when the name held nothing.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<data.@this> Remove(string name)
    {
        if (!_variables.TryGetValue(name, out var held)) return _context.NotFound(name);
        if (await Before(Events?.remove, name, held) is { } refused) return refused;
        if (!_variables.TryRemove(name, out var removed)) return _context.NotFound(name);
        return Events?.remove is { after.Count: > 0 } remove ? await remove.After(Named(name), removed, _context) : removed;
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

    /// <summary>The variables this memory holds, as a list — <c>%!app.variable.list%</c>: one variable per
    /// name, the current call's first (they shadow the actor's), a setting (<c>!</c>) left out. Born on
    /// each read; the memory itself is the one store.</summary>
    public global::app.type.item.list.@this<global::app.type.item.variable.@this> list
    {
        get
        {
            var names = (Calls.Current?.Names ?? []).Concat(_variables.Keys)
                .Where(n => !n.StartsWith('!'))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            var held = new global::app.type.item.list.@this<global::app.type.item.variable.@this>();
            foreach (var name in names)
                // a name the parser doesn't read back whole (stored by C#, not written in plang) isn't
                // one a program can name
                if (new parser.@this(name).Whole is { } variable)
                    held.Add(variable);
            return held;
        }
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
        // what a read sees: the current call's names first (they shadow the actor's), then the memory
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Calls.Current is { } frame)
            foreach (var name in frame.Names)
                if (!name.StartsWith('!') && seen.Add(name) && frame.TryGet(name, out var framed))
                    yield return new(name, framed);
        foreach (var kvp in _variables.Where(kvp => !kvp.Key.StartsWith("!")).OrderByDescending(kvp => kvp.Value.Updated))
            if (seen.Add(kvp.Key)) yield return kvp;
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
