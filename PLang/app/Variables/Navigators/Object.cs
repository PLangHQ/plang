using System.Reflection;

namespace app.Variables.Navigators;

/// <summary>
/// Navigates CLR objects via reflection. Lowest priority fallback.
/// </summary>
public sealed class Object : INavigator
{
    public bool CanNavigate(Data.@this data) => data.Value != null;

    public Data.@this Navigate(Data.@this data, string key)
    {
        var value = data.Value;
        if (value == null) return Data.@this.NotFound(key);

        // Walk the inheritance chain bottom-up so properties shadowed in a
        // derived type win over the base — GetProperty(name, IgnoreCase) throws
        // AmbiguousMatchException when both declare the same name (e.g.
        // AssertionError.Variables shadows IError.Variables).
        PropertyInfo? prop = null;
        for (var t = value.GetType(); t != null && prop == null; t = t.BaseType)
        {
            prop = t.GetProperty(key,
                BindingFlags.Public | BindingFlags.Instance |
                BindingFlags.IgnoreCase | BindingFlags.DeclaredOnly);
        }
        if (prop == null) return Data.@this.NotFound(key);

        try
        {
            return new Data.@this(key, prop.GetValue(value), parent: data);
        }
        catch (TargetInvocationException ex)
        {
            return Data.@this.FromError(new Errors.ServiceError(
                $"Failed to read '{key}': {(ex.InnerException ?? ex).Message}",
                "NavigationError", 500) { Exception = ex });
        }
    }
}
