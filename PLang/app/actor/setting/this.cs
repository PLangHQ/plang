using System.Collections.Concurrent;

namespace app.actor.setting;

/// <summary>Which backing a Get/Set targets: this-run memory, or the persistent store.</summary>
public enum Storage { InMemory, Persistent }

/// <summary>
/// An actor's settings. Holds both lifetimes behind <see cref="Storage"/>: in-memory (this run — the
/// <c>%!x%</c> + action-param cascade) and persistent (the app's store). Each actor has one
/// (<c>app.System.Setting</c>, <c>app.User.Setting</c>); the user's falls back to the system's. A
/// context's own layer (<c>context.Setting</c>) chains up to its actor's, so a read walks
/// this → parent → … → the actor's → the system's, and a goal-local setting shadows the rest.
/// </summary>
public sealed class @this
{
    internal const string Table = "settings";                       // store table name (data-compat)
    private readonly @this? _parent;
    private readonly actor.context.@this _context;                  // born-with-context (not-found Data + persistent reach)
    private readonly ConcurrentDictionary<string, data.@this> _values = new(StringComparer.OrdinalIgnoreCase);

    public @this(actor.context.@this context, @this? parent = null)
    { _context = context; _parent = parent; }

    private @this Root => _parent?.Root ?? this;                    // persistent resolves at the root (the system's)

    /// <summary>
    /// The one reader — storage is the switch, the value is always Data. InMemory walks the scope
    /// chain (this → parent → the system's); Persistent reads the store. <paramref name="keys"/> are
    /// tried most-specific first (InMemory: <c>module.action.param</c> then <c>module.param</c>;
    /// Persistent: the path).
    /// </summary>
    public ValueTask<data.@this> Get(Storage storage, params string[] keys)
        => storage == Storage.InMemory ? new(InMemory(keys)) : Persistent(keys);

    private data.@this InMemory(string[] keys)
    {
        for (@this? s = this; s != null; s = s._parent)
            foreach (var key in keys)
                if (s._values.TryGetValue(key, out var hit)) return hit;
        return _context.NotFound(keys.Length > 0 ? keys[0] : "setting");   // unset everywhere → seam falls to [Default]
    }

    private async ValueTask<data.@this> Persistent(string[] keys)
    {
        var path = keys.Length > 0 ? keys[0] : "";
        if (string.IsNullOrEmpty(path)) return _context.NotFound("setting");

        var dot = path.IndexOf('.');
        var key = dot >= 0 ? path[..dot] : path;
        var remaining = dot >= 0 ? path[(dot + 1)..] : null;

        var result = await (await Root._context.App.store).Get<global::app.type.item.@this>(Table, key);
        if (!result.Success) return result;

        var value = await result.Value();
        if (value is null || await value.IsEmpty())                        // unset → ASK (prompt user), not [Default]
            return _context.Error(new error.AskError($"Setting '{key}' is not set.", Table, key));

        return string.IsNullOrEmpty(remaining) ? result : await result.Get(remaining);
    }

    /// <summary>The one writer — mirror of <see cref="Get"/>. Stores the whole Data (keeps its type/props).</summary>
    public ValueTask<data.@this> Set(Storage storage, string key, data.@this? value)
        => storage == Storage.InMemory ? new(SetInMemory(key, value)) : SetPersistent(key, value);

    private data.@this SetInMemory(string key, data.@this? value)
    {
        if (value is null) { _values.TryRemove(key, out _); return _context.Ok(); }
        _values[key] = value;
        return value;
    }

    private async ValueTask<data.@this> SetPersistent(string key, data.@this? value)
        => await (await Root._context.App.store).Set(Table, key, value ?? _context.Ok());

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
            var instance = kind.Create();
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
                // an option of the action: this run's (the action's, then the module's), else its default
                if (hop.Length == 3 && action.Property.FirstOrDefault(p => string.Equals(p.Name, hop[2], StringComparison.OrdinalIgnoreCase)) is { } row)
                {
                    var run = InMemory([$"{hop[0]}.{hop[1]}.{hop[2]}", $"{hop[0]}.{hop[2]}"]);
                    return run.IsInitialized ? run : new data.@this(hop[2], row.Default, context: _context);
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
