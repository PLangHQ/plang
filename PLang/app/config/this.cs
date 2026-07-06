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
    /// Returns a context-bound view for a specific IConfig type.
    /// The view resolves property values through the scope chain for the given context.
    /// </summary>
    public ModuleView<T> For<T>(actor.context.@this context) where T : IConfig, new()
    {
        return new ModuleView<T>(this, context, ResolvePrefix<T>());
    }

    /// <summary>
    /// Applies non-null properties from a source object to the settings scope.
    /// Matches source properties against TConfig property names, writes with module prefix.
    /// Replaces manual if-null-set chains in provider Configure methods.
    /// </summary>
    public void Apply<TConfig>(object source, actor.context.@this context, bool isDefault = false)
        where TConfig : IConfig, new()
    {
        var prefix = ResolvePrefix<TConfig>();
        var configProps = typeof(TConfig).GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in source.GetType().GetProperties())
        {
            if (!configProps.Contains(prop.Name)) continue;

            // Action properties are Data<T> — unwrap to plain value for the scope chain.
            // Born-native scalars ride as their wrapper (bool→bool.@this); the config
            // store and Resolve<T> want the raw CLR value, so collapse the leaf.
            var raw = prop.GetValue(source);
            object? value = raw is data.@this data ? data.Peek() : raw;
            if (value is global::app.type.item.@this leaf) value = leaf.Clr<object>();
            if (value == null) continue;

            Set($"{prefix}.{prop.Name}", value, context, isDefault);
        }
    }

    private static string ResolvePrefix<TConfig>()
    {
        var fullName = typeof(TConfig).Namespace ?? "";
        var lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 ? fullName[(lastDot + 1)..] : fullName;
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
