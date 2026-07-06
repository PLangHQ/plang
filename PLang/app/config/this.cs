using app.actor.context;

namespace app.config;

/// <summary>
/// App-level settings registry. Owns:
/// - Module type registration (which IConfig types exist)
/// - App-level default scope (persistent across goals)
/// - Resolution logic: context scope → parent scope → app defaults → class defaults
///
/// Navigation: app.config.For&lt;archive.Config&gt;(context).Max
/// </summary>
public sealed class @this
{
    /// <summary>
    /// Resolves a setting value through the context's Setting door (walks the context chain
    /// current → Parent → base), then the class default. There is no separate app-default
    /// store — an app-wide setting lives on the base context of the chain (see <see cref="Set"/>).
    /// </summary>
    public T Resolve<T>(string key, actor.context.@this context, T classDefault)
    {
        var value = context.Setting.Resolve(key);
        return value != null ? Cast<T>(value, classDefault) : classDefault;
    }

    private static T Cast<T>(object value, T fallback)
    {
        if (value is T typed) return typed;
        try
        {
            var target = typeof(T);
            if (target.IsEnum)
            {
                if (value is string s && Enum.TryParse(target, s, ignoreCase: true, out var parsed))
                    return (T)parsed;
                return (T)Enum.ToObject(target, value);
            }
            return (T)Convert.ChangeType(value, target);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
        { return fallback; }
    }

    /// <summary>
    /// Writes a setting. A bare (goal) write lands on the current context and shadows via the
    /// up-walk. An app-wide write (<paramref name="isDefault"/> / "on app") lands on the BASE of
    /// the current chain — the actor's context; in an async/parallel sub-tree, the context above
    /// the spawned children — so every context in the chain resolves it.
    /// </summary>
    public void Set(string key, object value, actor.context.@this context, bool isDefault = false)
    {
        var target = isDefault ? Base(context) : context;
        target.Setting.Set(key, value);
    }

    private static actor.context.@this Base(actor.context.@this context)
    {
        while (context.Parent != null) context = context.Parent;
        return context;
    }
}
