using System.Collections.Concurrent;

namespace app.actor.setting;

/// <summary>
/// Settings, in layers: a class's defaults ← the actor's saved row (the user's, else the system's) ←
/// this run's values (<c>set %!x%</c>, the CLI flags) ← the step's own. Each actor has one
/// (<c>app.System.Setting</c>, <c>app.User.Setting</c>, the user's falling back to the system's) that holds
/// the actor's rows; a context's own layer (<c>context.Setting</c>) chains up to its actor's, so a
/// goal-local value shadows the rest. The rows are read once, when the app starts (<see cref="Load"/>);
/// after that a setting is built in memory.
/// </summary>
public sealed class @this
{
    internal const string Table = "settings";                       // the store's table: one row per actor per setting
    private readonly @this? _parent;
    private readonly actor.context.@this _context;                  // born-with-context (not-found Data, the store's reach)
    private readonly ConcurrentDictionary<string, data.@this> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly global::app.actor.@this? _actor;               // an actor's own: the actor whose rows it holds
    private readonly Lazy<Task<ConcurrentDictionary<string, data.@this>>>? _rows;
    private ConcurrentDictionary<string, data.@this>? _held;        // the rows, once read
    private global::app.error.Error? _unread;                       // why the rows could not be read, when they couldn't
    // While the rows are being read, a setting read inside that (the store reading a row back) builds
    // without them — it can't wait on the read it is part of.
    private readonly AsyncLocal<bool> _reading = new();

    /// <summary>A context's layer, chaining up to <paramref name="parent"/>.</summary>
    public @this(actor.context.@this context, @this? parent = null)
    { _context = context; _parent = parent; }

    /// <summary>An actor's own: holds the actor's saved rows, falling back to <paramref name="parent"/>
    /// (the system's, for the user).</summary>
    public @this(global::app.actor.@this actor, @this? parent) : this(actor.Context, parent)
    {
        _actor = actor;
        _rows = new(Read);
    }

    /// <summary>A value under a setting's path was written — this run's, or a saved row. What holds a
    /// setting it reads on every step (Debug, a call stack) builds it again. Raised on each actor's own
    /// settings up the chain.</summary>
    internal event Action<string>? Written;

    /// <summary>Reads the saved rows of every actor up the chain, once — the app does it when it starts.
    /// After it a setting is built in memory. Rows that could not be read are the error (a setting then
    /// builds without them); a save refuses to write over them.</summary>
    public async Task<data.@this> Load()
    {
        for (@this? s = this; s != null; s = s._parent)
            if (s._rows != null && !s._reading.Value) s._held = await s._rows.Value;
        for (@this? s = this; s != null; s = s._parent)
            if (s._unread != null) return _context.Error(s._unread);
        return _context.Ok();
    }

    /// <summary>
    /// An action's option as the settings hold it — the action-param seam's rung and
    /// <c>%!llm.query.cache%</c>'s: this run's value (the action's key <c>llm.query.cache</c>, then the
    /// module's <c>llm.cache</c>), the closest scope first; else the saved rows (the action's row, then the
    /// module's own), the user's before the system's. NotFound when none.
    /// </summary>
    public async ValueTask<data.@this> Get(global::app.goal.step.action.@this action, string option)
    {
        await Load();
        var module = action.Module.Name;
        var run = Run([$"{module}.{action.Name}.{option}", $"{module}.{option}"]);
        if (run.IsInitialized) return run;
        if (Saved($"{module}.{action.Name}") is { } row && Option(row, option) is { } saved) return saved;
        if (Saved(module) is { } own && Option(own, option) is { } kept) return kept;
        return _context.NotFound($"{module}.{action.Name}.{option}");
    }

    // This run's value for the first key that has one, the closest scope first.
    private data.@this Run(string[] keys)
    {
        for (@this? s = this; s != null; s = s._parent)
            foreach (var key in keys)
                if (s._values.TryGetValue(key, out var hit)) return hit;
        return _context.NotFound(keys.Length > 0 ? keys[0] : "setting");
    }

    /// <summary>This run's value for <paramref name="key"/> in this scope; null clears it.</summary>
    public ValueTask<data.@this> Set(string key, data.@this? value)
    {
        if (value is null) _values.TryRemove(key, out _);
        else _values[key] = value;
        Tell(key);
        return new(value ?? _context.Ok());
    }

    /// <summary>
    /// This run's values for the setting class at <paramref name="path"/>, from a CLI flag's dict
    /// (<c>--test={"timeoutSeconds":5}</c> → <c>app.test.setting.timeoutSeconds</c>); a nested dict lands one
    /// level deeper. A key that isn't one of the class's options, or a value its option can't take, is
    /// refused — nothing is written then.
    /// </summary>
    public data.@this Set(string path, IDictionary<string, object?> values)
    {
        if (Class(path)?.Create() is not { } sample)
            return _context.Error(new global::app.error.Error($"'{path}' names no setting class.", "UnknownSetting", 400));
        var fits = sample.Apply(values, _context);
        if (!fits.Success) return fits;
        Flatten(path, values);
        Tell(path);
        return _context.Ok();
    }

    private void Flatten(string path, IDictionary<string, object?> values)
    {
        foreach (var (key, value) in values)
        {
            if (value is IDictionary<string, object?> nested) Flatten($"{path}.{key}", nested);
            else _values[$"{path}.{key}"] = new data.@this(key, value, context: _context);
        }
    }

    /// <summary>Stores <paramref name="value"/> — a setting whole, or an action — as the row for
    /// <paramref name="path"/> of the actor this scope belongs to.</summary>
    public async ValueTask<data.@this> Save(string path, data.@this value)
    {
        // a node that leads to settings (%!http%, %!llm.query%) is no class: its row could not be read back
        if (value.Peek() is global::app.type.item.setting.@this { } setting && Class(setting.Path) == null)
            return _context.Error(new global::app.error.Error(
                $"'{setting.Path}' is not a setting class — save the class that holds the option.", "NotASettingClass", 400));
        var owner = Owner;
        // rows that could not be read are never written over
        if (await owner.Load() is { Success: false } unread) return unread;
        var row = new data.@this($"{owner._actor!.Name.ToLowerInvariant()}!{path}", value.Peek(), value.Type, context: _context);
        var stored = await (await _context.App.store).Set(Table, row.Name, row);
        if (!stored.Success) return stored;
        owner._held![path] = row;
        Tell(path);
        return row;
    }

    /// <summary>Deletes the actor's row for <paramref name="path"/> — back to the system's row, or the
    /// defaults.</summary>
    public async ValueTask<data.@this> Remove(string path)
    {
        var owner = Owner;
        if (await owner.Load() is { Success: false } unread) return unread;
        var removed = await (await _context.App.store).Remove(Table, $"{owner._actor!.Name.ToLowerInvariant()}!{path}");
        if (!removed.Success) return removed;
        owner._held!.TryRemove(path, out _);
        Tell(path);
        return removed;
    }

    // Every actor's own settings up the chain hears a write — the one place what holds a setting learns it.
    private void Tell(string key)
    {
        for (@this? s = this; s != null; s = s._parent)
            s.Written?.Invoke(key);
    }

    // The actor's own settings this scope chains up to — the one whose rows a save writes.
    private @this Owner
    {
        get
        {
            for (var s = this; s != null; s = s._parent)
                if (s._actor != null) return s;
            throw new InvalidOperationException("these settings belong to no actor — there is no row to save");
        }
    }

    // The saved row for path, the closest actor first (the user's, then the system's); null when none
    // (or before the rows are read). An actor's own class reads that actor's row alone.
    private data.@this? Saved(string path)
    {
        for (@this? s = this; s != null; s = s._parent)
        {
            if (s._held != null && s._held.TryGetValue(path, out var row)) return row;
            if (s._actor != null && Own(path)) return null;
        }
        return null;
    }

    // Whether the class at path is an actor's own — its values never come from the actor it falls back to.
    private bool Own(string path) => Class(path)?.Own == true;

    // One option of a saved row: a setting's (its property), or an action's (its Property row).
    private data.@this? Option(data.@this row, string name) => row.Peek() switch
    {
        global::app.type.item.setting.@this setting when setting.Option(name) is { } option
            => new data.@this(name, option.GetValue(setting), context: _context),
        global::app.goal.step.action.@this action
            when action.Property.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) is { } property
            => new data.@this(name, property.Value, context: _context),
        _ => null,
    };

    // The actor's rows, read from the app's store — its keys are "<actor>!<path>" — each value read
    // through, so a setting builds from them in memory.
    private async Task<ConcurrentDictionary<string, data.@this>> Read()
    {
        var rows = new ConcurrentDictionary<string, data.@this>(StringComparer.OrdinalIgnoreCase);
        var prefix = _actor!.Name.ToLowerInvariant() + "!";
        _reading.Value = true;
        global::app.store.@this store;
        // A store that can't open (an unwritable root) holds no rows: nothing was ever saved there.
        try { store = await _context.App.store; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            await (_context.App.Debug?.Write($"settings: the store could not open, so the {_actor.Name} actor has no saved rows — {ex.Message}") ?? Task.CompletedTask);
            return rows;
        }
        var all = await store.GetAll<global::app.type.item.@this>(Table);
        if (!all.Success)
        {
            await (_context.App.Debug?.Write($"settings: the {_actor.Name} actor's rows could not be read — {all.Error?.Message}") ?? Task.CompletedTask);
            _unread = all.Error ?? new global::app.error.Error($"the {_actor.Name} actor's saved settings could not be read", "SettingsUnreadable", 500);
            return rows;
        }
        foreach (var row in (await all.Value())!.Items(_context))
            if (row.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && await row.Value() is { IsNull: false } value)
                rows[row.Name[prefix.Length..]] = new data.@this(row.Name, value, row.Type, context: _context);
        return rows;
    }

    /// <summary>
    /// The setting class at <paramref name="path"/>, as this scope sees it — its defaults, the actor's
    /// saved row on them (the user's, else the system's; none before the app reads them), this run's
    /// values on top. What an owner reads its settings through (<c>test.start</c>, the build, Debug).
    /// </summary>
    public global::app.type.item.setting.@this this[string path]
    {
        get
        {
            var kind = Class(path) ?? throw new KeyNotFoundException($"'{path}' names no setting class.");
            var instance = kind.Create();
            if (Saved(path)?.Peek() is global::app.type.item.setting.@this held)
                foreach (var option in held.Options) option.SetValue(instance, option.GetValue(held));
            // this run's values for its own options — a longer path under it (llm.query.cache under llm)
            // is another setting's
            var own = Under(path).Where(kv => instance.Option(kv.Key) != null).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
            var applied = instance.Apply(own, _context);
            if (!applied.Success) throw new InvalidOperationException(applied.Error!.Message);
            return instance;
        }
    }

    /// <summary>The setting class <typeparamref name="T"/>, as this scope sees it — the class says its own
    /// path (<c>context.Setting.Of&lt;test.setting&gt;()</c>).</summary>
    public T Of<T>() where T : global::app.type.item.setting.@this, new() => (T)this[new T().Path];

    /// <summary>The settings <paramref name="owner"/> names with <c>ISetting&lt;T&gt;</c>, as this scope sees
    /// them — <c>%!app.goal.list.setting%</c>, <c>%!app.setting%</c>; null when it names none.</summary>
    public async ValueTask<data.@this?> Of(object owner)
    {
        var named = owner.GetType().GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(global::app.type.item.setting.ISetting<>));
        if (named == null) return null;
        await Load();
        return Instance(((global::app.type.item.setting.@this)Activator.CreateInstance(named.GetGenericArguments()[0])!).Path);
    }

    // The class of settings at path, when there is one.
    private global::app.type.item.setting.kind.@this? Class(string path)
        => _context.App.type.list["setting"].kind[path] as global::app.type.item.setting.kind.@this;

    // The class at path as a value — a value its options can't take (this run's) is the error.
    private data.@this Instance(string path)
    {
        try { return new data.@this(path, this[path], context: _context); }
        catch (InvalidOperationException ex) { return _context.Error(new global::app.error.Error(ex.Message, "TypeConversionFailed", 400)); }
    }

    /// <summary>
    /// The first setting a <c>!</c> name the memory doesn't bind names (<c>%!llm%</c>, <c>%!http%</c>): the
    /// module's settings — its own class when it has one — which answer the next hop themselves (an action,
    /// then its options). NotFound when the name is no module's. (An owner's settings are reached through
    /// the owner: <c>%!app.goal.list.setting%</c>.)
    /// </summary>
    public async ValueTask<data.@this> Get(string name)
    {
        // a name that is no module's names no setting: unset, as an unbound %!x% has always read (a
        // template leaves it as written, the ask sentinel reads its absence)
        if (!(await _context.App.module.Get(name)).Success) return _context.NotFound(name);
        await Load();
        return Class(name) != null
            ? Instance(name)
            : new data.@this(name, new global::app.type.item.setting.module.@this(name), context: _context);
    }

    // This run's values under path, the closest scope winning, as the options they set — a key deeper than
    // an option (path.llm.system) nests under it. An actor's own class stops at that actor's scope.
    private Dictionary<string, object?> Under(string path)
    {
        var under = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var prefix = path + ".";
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var own = Own(path);
        for (@this? s = this; s != null; s = own && s._actor != null ? null : s._parent)
            foreach (var (key, value) in s._values)
            {
                if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !seen.Add(key)) continue;
                var at = under;
                var names = key[prefix.Length..].Split('.');
                for (var i = 0; i < names.Length - 1; i++)
                {
                    if (!at.TryGetValue(names[i], out var inner) || inner is not Dictionary<string, object?> deeper)
                        at[names[i]] = deeper = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    at = deeper;
                }
                at.TryAdd(names[^1], value.Peek());
            }
        return under;
    }

    /// <summary>An independent copy of this level; keeps the same parent link + context.</summary>
    public @this Clone()
    {
        var clone = new @this(_context, _parent);
        foreach (var kvp in _values) clone._values[kvp.Key] = kvp.Value;
        return clone;
    }
}
