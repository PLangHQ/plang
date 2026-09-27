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
    /// After it a setting is built in memory.</summary>
    public async Task Load()
    {
        for (@this? s = this; s != null; s = s._parent)
            if (s._rows != null && !s._reading.Value) s._held = await s._rows.Value;
    }

    /// <summary>
    /// The action-param seam's door: this run's value for the first of <paramref name="keys"/> that has
    /// one, the closest scope first; else the actor's rows (the user's, then the system's), key by key.
    /// The keys are the action's (<c>llm.query.cache</c>) then the module's (<c>llm.cache</c>). NotFound —
    /// the seam falls to the <c>[Default]</c> — when none.
    /// </summary>
    public async ValueTask<data.@this> Get(string[] keys)
    {
        await Load();
        var run = Run(keys);
        if (run.IsInitialized) return run;
        foreach (var key in keys)
        {
            var dot = key.LastIndexOf('.');
            if (dot > 0 && Saved(key[..dot]) is { } row && Option(row, key[(dot + 1)..]) is { } saved)
                return saved;
        }
        return _context.NotFound(keys.Length > 0 ? keys[0] : "setting");
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
        var fits = Apply(sample, values);
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
        var owner = Owner;
        await owner.Load();
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
        await owner.Load();
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
    // (or before the rows are read).
    private data.@this? Saved(string path)
    {
        for (@this? s = this; s != null; s = s._parent)
            if (s._held != null && s._held.TryGetValue(path, out var row)) return row;
        return null;
    }

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
            var applied = Apply(instance, own);
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
        return await Get(((global::app.type.item.setting.@this)Activator.CreateInstance(named.GetGenericArguments()[0])!).Path);
    }

    // The class of settings at path, when there is one.
    private global::app.type.item.setting.kind.@this? Class(string path)
        => (_context.App.type.list["setting"].kind as global::app.type.kind.empty.@this)?[path] as global::app.type.item.setting.kind.@this;

    /// <summary>
    /// The setting <paramref name="path"/> names, as this scope sees it (<c>%!path%</c>): a class's instance;
    /// an action's option (<c>llm.query.cache</c>) — as the seam reads it, else the action's default; a node
    /// for a path that leads to settings (a module, an action, a prefix of a class's path). NotFound when
    /// the path names none.
    /// </summary>
    public async ValueTask<data.@this> Get(string path)
    {
        await Load();
        if (Class(path) != null)
        {
            try { return new data.@this(path, this[path], context: _context); }
            catch (InvalidOperationException ex) { return _context.Error(new global::app.error.Error(ex.Message, "TypeConversionFailed", 400)); }
        }

        var hop = path.Split('.');
        if (_context.App.module.list.Items().FirstOrDefault(m => string.Equals(m.Name, hop[0], StringComparison.OrdinalIgnoreCase)) is { } module)
        {
            if (hop.Length == 1) return Node(path);
            if (module[hop[1]] is { } action)
            {
                if (hop.Length == 2) return Node(path);
                if (hop.Length == 3 && action.Property.FirstOrDefault(p => string.Equals(p.Name, hop[2], StringComparison.OrdinalIgnoreCase)) is { } row)
                {
                    var set = await Get([$"{hop[0]}.{hop[1]}.{hop[2]}", $"{hop[0]}.{hop[2]}"]);
                    return set.IsInitialized ? set : new data.@this(hop[2], row.Default, context: _context);
                }
            }
        }

        var classes = _context.App.type.list["setting"].kind as global::app.type.kind.empty.@this;
        if (classes?.kinds.Any(k => k.Name.StartsWith(path + ".", StringComparison.OrdinalIgnoreCase)) == true)
            return Node(path);
        return _context.NotFound(path);
    }

    // A path that leads to settings, as a value.
    private data.@this Node(string path) => new(path, new global::app.type.item.setting.@this(path), context: _context);

    // This run's values under path, the closest scope winning, as the options they set — a key deeper than
    // an option (path.llm.system) nests under it.
    private Dictionary<string, object?> Under(string path)
    {
        var under = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var prefix = path + ".";
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (@this? s = this; s != null; s = s._parent)
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

    /// <summary>
    /// Applies raw values onto <paramref name="node"/>'s public settable properties — the convert walk a
    /// setting takes this run's values (and a saved row its options) through. Each leaf converts through
    /// the plang catalog; a nested dict onto an owned composite descends field-by-field, constructing the
    /// child if absent.
    /// </summary>
    internal data.@this Apply(object node, IDictionary<string, object?> settings)
    {
        foreach (var kvp in settings)
        {
            var prop = node.GetType().GetProperty(kvp.Key,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (prop?.SetMethod?.IsPublic != true)
                return _context.Error(new global::app.error.Error(
                    $"Unknown setting '{kvp.Key}' on {node.GetType().Name} — no public-settable property.",
                    "UnknownSetting", 400));

            if (kvp.Value is IDictionary<string, object?> sub && IsComposite(prop.PropertyType))
            {
                var child = prop.GetValue(node) ?? Construct(prop.PropertyType);
                var r = Apply(child, sub);
                if (!r.Success) return r;
                prop.SetValue(node, child);
            }
            else
            {
                // Lift the raw setting to its plang value. A plang-typed property (a native
                // list<path>, a number) stores the plang value DIRECTLY — no Clr boundary to cross; it
                // holds lazy and materializes at the CONSUMER's door (row.Value<path>()). Only a CLR
                // slot (bool/string) or a typed-generic plang slot the born native value can't fit
                // lowers via Clr (the value owns its projection).
                object? val;
                try
                {
                    var built = global::app.type.item.@this.Create(kvp.Value, _context);
                    // a plang-typed slot the born value doesn't fit (a choice from its text) is made by
                    // the slot's own type, through its own Create
                    if (typeof(global::app.type.item.@this).IsAssignableFrom(prop.PropertyType)
                        && !prop.PropertyType.IsInstanceOfType(built)
                        && prop.PropertyType.GetMethod("Create", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                               [typeof(object), typeof(data.@this)]) is { } create
                        && create.Invoke(null, [kvp.Value, new data.@this(kvp.Key, context: _context)]) is global::app.type.item.@this made
                        && prop.PropertyType.IsInstanceOfType(made))
                        built = made;
                    val = typeof(global::app.type.item.@this).IsAssignableFrom(prop.PropertyType)
                          && prop.PropertyType.IsInstanceOfType(built)
                        ? built
                        : built.Clr(prop.PropertyType);
                }
                catch (System.Exception ex) when (ex is System.InvalidCastException or System.FormatException
                    or System.OverflowException or System.NotSupportedException)
                {
                    return _context.Error(new global::app.error.Error(
                        $"setting '{kvp.Key}' cannot bind to {prop.PropertyType.Name}: {ex.Message}",
                        "TypeConversionFailed", 400) { Exception = ex });
                }
                prop.SetValue(node, val);
            }
        }
        return _context.Ok();
    }

    /// <summary>Descend into a class with public setters that isn't a plang leaf (string/primitive/enum/collection).</summary>
    private static bool IsComposite(System.Type t)
    {
        var u = System.Nullable.GetUnderlyingType(t) ?? t;
        if (u.IsPrimitive || u.IsEnum || u == typeof(string) || u == typeof(decimal)) return false;
        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(u)) return false;
        if (!u.IsClass) return false;
        foreach (var p in u.GetProperties())
            if (p.SetMethod?.IsPublic == true) return true;
        return false;
    }

    /// <summary>Construct a null composite: subsystem nodes take a context; config records are parameterless.</summary>
    private object Construct(System.Type t)
    {
        var withContext = t.GetConstructor(new[] { typeof(actor.context.@this) });
        return withContext != null
            ? withContext.Invoke(new object[] { _context })
            : System.Activator.CreateInstance(t)!;
    }

    public bool Contains(string key) => _values.ContainsKey(key);

    /// <summary>An independent copy of this level; keeps the same parent link + context.</summary>
    public @this Clone()
    {
        var clone = new @this(_context, _parent);
        foreach (var kvp in _values) clone._values[kvp.Key] = kvp.Value;
        return clone;
    }
}
