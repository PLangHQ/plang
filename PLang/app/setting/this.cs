using System.Collections.Concurrent;

namespace app.setting;

/// <summary>Which backing a Get/Set targets: this-run memory, or the persistent sqlite store.</summary>
public enum Storage { InMemory, Persistent }

/// <summary>
/// The single setting authority. Holds BOTH lifetimes behind <see cref="Storage"/>:
/// in-memory (this run — the <c>%!x%</c> + action-param cascade) and persistent (sqlite —
/// <c>%setting.X%</c>). Reached app-level as <c>app.Setting</c> (the chain root, <c>_parent == null</c>)
/// and scoped as <c>context.Setting</c> (chains up to the app root). A read walks
/// this → parent → … → app root, so a goal-local setting shadows an app-level one.
/// </summary>
public sealed class @this
{
    internal const string Table = "settings";                       // sqlite table name (data-compat)
    private readonly @this? _parent;
    private readonly actor.context.@this _context;                  // born-with-context (not-found Data + persistent reach)
    private readonly ConcurrentDictionary<string, data.@this> _values = new(StringComparer.OrdinalIgnoreCase);

    public @this(actor.context.@this context, @this? parent = null)
    { _context = context; _parent = parent; }

    private @this Root => _parent?.Root ?? this;                    // persistent resolves at the root (holds the store)

    /// <summary>
    /// The one reader — storage is the switch, the value is always Data. InMemory walks the scope
    /// chain (this → parent → app root); Persistent reads sqlite. <paramref name="keys"/> are tried
    /// most-specific first (InMemory: <c>module.action.param</c> then <c>module.param</c>; Persistent: the path).
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

        var result = await (await Root._context.App.SettingsStore).Get<global::app.type.item.@this>(Table, key);
        if (!result.Success) return result;

        var value = await result.Value();
        if (value is null || await value.IsEmpty())                        // unset → ASK (prompt user), not [Default]
            return _context.Error(new error.AskError($"Setting '{key}' is not set.", Table, key));

        return string.IsNullOrEmpty(remaining) ? result : await result.GetChild(remaining);
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
        => await (await Root._context.App.SettingsStore).Set(Table, key, value ?? _context.Ok());

    public bool Contains(string key) => _values.ContainsKey(key);

    /// <summary>An independent copy of this level; keeps the same parent link + context.</summary>
    public @this Clone()
    {
        var clone = new @this(_context, _parent);
        foreach (var kvp in _values) clone._values[kvp.Key] = kvp.Value;
        return clone;
    }
}
