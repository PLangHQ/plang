using System.Collections.Concurrent;

namespace app.actor.setting;

/// <summary>
/// Settings, in layers: a class's defaults ← the actor's saved row (the user's, else the system's) ←
/// this run's values (<c>set %!x%</c>, the CLI flags) ← the step's own. Each actor has one
/// (<c>app.System.Setting</c>, <c>app.User.Setting</c>, the user's falling back to the system's) that holds
/// the actor's rows; a context's own layer (<c>context.Setting</c>) chains up to its actor's, so a
/// goal-local value shadows the rest.
/// </summary>
public sealed class @this
{
    internal const string Table = "settings";                       // the store's table: one row per actor per setting
    private readonly @this? _parent;
    private readonly actor.context.@this _context;                  // born-with-context (not-found Data, the store's reach)
    private readonly ConcurrentDictionary<string, data.@this> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly global::app.actor.@this? _actor;               // an actor's own: the actor whose rows it holds
    private readonly Lazy<Task<ConcurrentDictionary<string, data.@this>>>? _rows;

    /// <summary>A context's layer, chaining up to <paramref name="parent"/>.</summary>
    public @this(actor.context.@this context, @this? parent = null)
    { _context = context; _parent = parent; }

    /// <summary>An actor's own: holds the actor's saved rows (read from the app's store on first use),
    /// falling back to <paramref name="parent"/> (the system's, for the user).</summary>
    public @this(global::app.actor.@this actor, @this? parent) : this(actor.Context, parent)
    {
        _actor = actor;
        _rows = new(Load);
    }

    /// <summary>
    /// The action-param seam's door: this run's value for the first of <paramref name="keys"/> that has
    /// one, the closest scope first; else the actor's rows (the user's, then the system's), key by key.
    /// The keys are the action's (<c>llm.query.cache</c>) then the module's (<c>llm.cache</c>). NotFound —
    /// the seam falls to the <c>[Default]</c> — when none.
    /// </summary>
    public async ValueTask<data.@this> Get(string[] keys)
    {
        var run = Run(keys);
        if (run.IsInitialized) return run;
        foreach (var key in keys)
        {
            var dot = key.LastIndexOf('.');
            if (dot > 0 && await Saved(key[..dot]) is { } row && await Option(row, key[(dot + 1)..]) is { } saved)
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
        if (value is null) { _values.TryRemove(key, out _); return new(_context.Ok()); }
        _values[key] = value;
        return new(value);
    }

    /// <summary>Stores <paramref name="value"/> — a setting whole, or an action — as the row for
    /// <paramref name="path"/> of the actor this scope belongs to.</summary>
    public async ValueTask<data.@this> Save(string path, data.@this value)
    {
        var owner = Owner;
        var row = new data.@this($"{owner._actor!.Name.ToLowerInvariant()}!{path}", value.Peek(), value.Type, context: _context);
        var stored = await (await _context.App.store).Set(Table, row.Name, row);
        if (stored.Success) (await owner._rows!.Value)[path] = row;
        return stored.Success ? row : stored;
    }

    /// <summary>Deletes the actor's row for <paramref name="path"/> — back to the system's row, or the
    /// defaults.</summary>
    public async ValueTask<data.@this> Remove(string path)
    {
        var owner = Owner;
        var removed = await (await _context.App.store).Remove(Table, $"{owner._actor!.Name.ToLowerInvariant()}!{path}");
        if (removed.Success) (await owner._rows!.Value).TryRemove(path, out _);
        return removed;
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

    // The saved row for path, the closest actor first (the user's, then the system's); null when none.
    private async ValueTask<data.@this?> Saved(string path)
    {
        for (@this? s = this; s != null; s = s._parent)
            if (s._rows != null && (await s._rows.Value).TryGetValue(path, out var row)) return row;
        return null;
    }

    // One option of a saved row: a setting's (its property), or an action's (its Property row).
    private async ValueTask<data.@this?> Option(data.@this row, string name) => await row.Value() switch
    {
        global::app.type.item.setting.@this setting when setting.Option(name) is { } option
            => new data.@this(name, option.GetValue(setting), context: _context),
        global::app.goal.step.action.@this action
            when action.Property.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) is { } property
            => new data.@this(name, property.Value, context: _context),
        _ => null,
    };

    // The actor's rows, read once from the app's store: its keys are "<actor>!<path>".
    private async Task<ConcurrentDictionary<string, data.@this>> Load()
    {
        var rows = new ConcurrentDictionary<string, data.@this>(StringComparer.OrdinalIgnoreCase);
        var prefix = _actor!.Name.ToLowerInvariant() + "!";
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
            if (row.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                rows[row.Name[prefix.Length..]] = row;
        return rows;
    }

    /// <summary>
    /// Applies a raw settings dict onto <paramref name="node"/>'s public-settable properties (the CLI
    /// <c>--flag={…}</c> convert-walk). Each leaf converts through the plang catalog (<c>TryConvert</c>);
    /// a nested dict onto an owned composite descends field-by-field, constructing the child if absent.
    /// Public-setter gate = exposure is the access level.
    /// </summary>
    public data.@this Set(object node, System.Collections.Generic.IDictionary<string, object?> settings)
    {
        foreach (var kvp in settings)
        {
            var prop = node.GetType().GetProperty(kvp.Key,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (prop?.SetMethod?.IsPublic != true)
                return _context.Error(new global::app.error.Error(
                    $"Unknown setting '{kvp.Key}' on {node.GetType().Name} — no public-settable property.",
                    "UnknownSetting", 400));

            if (kvp.Value is System.Collections.Generic.IDictionary<string, object?> sub && IsComposite(prop.PropertyType))
            {
                var child = prop.GetValue(node) ?? Construct(prop.PropertyType);
                var r = Set(child, sub);
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

    /// <summary>
    /// The setting <paramref name="path"/> names, as this scope sees it (<c>%!path%</c>): a class's instance
    /// — its defaults, this run's values on it; an action's option (<c>llm.query.cache</c>) — this run's
    /// value, else the action's default; a node for a path that leads to settings (<c>goal</c>,
    /// <c>goal.list</c>, a module, an action). NotFound when the path names none.
    /// </summary>
    public async ValueTask<data.@this> Get(string path)
    {
        var classes = _context.App.type.list["setting"].kind as global::app.type.kind.empty.@this;
        if (classes?[path] is global::app.type.item.setting.kind.@this kind)
        {
            // the defaults, the actor's saved row on them (the user's, else the system's), this run's on top
            var instance = kind.Create();
            if (await Saved(path) is { } saved && await saved.Value() is global::app.type.item.setting.@this held)
                foreach (var option in held.Options) option.SetValue(instance, option.GetValue(held));
            var run = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (option, value) in Under(path))
                if (instance.Option(option) != null) run[option] = await value.Value();
            var applied = Set(instance, run);
            return applied.Success ? new data.@this(path, instance, context: _context) : applied;
        }

        var hop = path.Split('.');
        if (_context.App.module.list.Items().FirstOrDefault(m => string.Equals(m.Name, hop[0], StringComparison.OrdinalIgnoreCase)) is { } module)
        {
            if (hop.Length == 1) return Node(path);
            if (module[hop[1]] is { } action)
            {
                if (hop.Length == 2) return Node(path);
                // an option of the action: as the seam reads it (this run's, the saved rows — the
                // action's, then the module's), else its default
                if (hop.Length == 3 && action.Property.FirstOrDefault(p => string.Equals(p.Name, hop[2], StringComparison.OrdinalIgnoreCase)) is { } row)
                {
                    var set = await Get([$"{hop[0]}.{hop[1]}.{hop[2]}", $"{hop[0]}.{hop[2]}"]);
                    return set.IsInitialized ? set : new data.@this(hop[2], row.Default, context: _context);
                }
            }
        }

        if (classes?.kinds.Any(k => k.Name.StartsWith(path + ".", StringComparison.OrdinalIgnoreCase)) == true)
            return Node(path);
        return _context.NotFound(path);
    }

    // A path that leads to settings, as a value.
    private data.@this Node(string path) => new(path, new global::app.type.item.setting.@this(path), context: _context);

    // This run's values under path (keys path.option, one level down), the closest scope winning.
    private Dictionary<string, data.@this> Under(string path)
    {
        var under = new Dictionary<string, data.@this>(StringComparer.OrdinalIgnoreCase);
        var prefix = path + ".";
        for (@this? s = this; s != null; s = s._parent)
            foreach (var (key, value) in s._values)
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && key.IndexOf('.', prefix.Length) < 0)
                    under.TryAdd(key[prefix.Length..], value);
        return under;
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
