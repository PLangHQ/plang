using System.Collections.Concurrent;

namespace app.setting;

/// <summary>
/// The in-memory settings that belong to a context — the door behind <c>context.Setting</c>
/// and the <c>%!%</c> sigil. Born with its context and linked to the parent context's door, so
/// a read walks <c>this → parent → … → root</c>: a goal-local setting shadows an app-level one,
/// and "applies to all subgoals" falls out of the up-walk (no snapshot taken at spawn).
///
/// Parallels <see cref="app.module.settings.@this"/> — the persistent <c>%setting.%</c> store —
/// same word, split by lifetime: this is in-memory/scoped, that is sqlite. The two hold
/// unrelated values.
/// </summary>
public sealed class @this
{
    private readonly @this? _parent;
    private readonly ConcurrentDictionary<string, object> _values =
        new(StringComparer.OrdinalIgnoreCase);

    public @this(@this? parent = null) => _parent = parent;

    /// <summary>
    /// Resolves a setting by walking levels <c>this → parent → … → root</c>. <paramref name="keys"/>
    /// are tried most-specific-first *within* each level (action-key before module-key), so
    /// scope/locality is primary and address specificity is the within-level tiebreak. Returns
    /// null when unset at every level.
    /// </summary>
    public object? Resolve(params string[] keys)
    {
        for (@this? s = this; s != null; s = s._parent)
            foreach (var key in keys)
                if (s._values.TryGetValue(key, out var value))
                    return value;
        return null;
    }

    /// <summary>Writes a setting at this level (its own goal scope).</summary>
    public void Set(string key, object? value)
    {
        if (value == null) { _values.TryRemove(key, out _); return; }
        _values[key] = value;
    }

    public bool Contains(string key) => _values.ContainsKey(key);

    /// <summary>An independent copy of this level; keeps the same parent link.</summary>
    public @this Clone()
    {
        var clone = new @this(_parent);
        foreach (var kvp in _values)
            clone._values[kvp.Key] = kvp.Value;
        return clone;
    }
}
