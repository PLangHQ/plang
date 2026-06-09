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
    /// App-level default scope. Values here persist across goal executions.
    /// Written when a settings action has Default=true.
    /// </summary>
    public Scope Defaults { get; } = new();

    /// <summary>
    /// Resolves a setting value by walking the scope chain:
    /// context.ConfigScope → context.Parent.ConfigScope → ... → Defaults → classDefault.
    /// </summary>
    public T Resolve<T>(string key, actor.context.@this context, T classDefault)
    {
        // Walk: context.ConfigScope → parent.ConfigScope → ... → Defaults → classDefault
        var current = context;
        while (current != null)
        {
            if (current.ConfigScope != null)
            {
                var value = current.ConfigScope.Get(key);
                if (value != null) return Cast<T>(value, classDefault);
            }
            current = current.Parent;
        }

        var defaultValue = Defaults.Get(key);
        if (defaultValue != null) return Cast<T>(defaultValue, classDefault);

        return classDefault;
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
            if (value is global::app.type.item.@this leaf) value = leaf.ToRaw();
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
    /// Writes a setting value to the appropriate scope.
    /// If isDefault is true, writes to app Defaults. Otherwise writes to the context's goal scope.
    /// </summary>
    public void Set(string key, object value, actor.context.@this context, bool isDefault = false)
    {
        if (isDefault) { Defaults.Set(key, value); return; }

        context.ConfigScope ??= new Scope();
        context.ConfigScope.Set(key, value);
    }
}
