using System.Reflection;

namespace App.Data.Navigators;

/// <summary>
/// Navigates CLR objects via reflection. Lowest priority fallback.
/// </summary>
public sealed class ObjectNavigator : INavigator
{
    public bool CanNavigate(Data.@this data) => data.Value != null;

    public Data.@this Navigate(Data.@this data, string key)
    {
        var value = data.Value;
        if (value == null) return Data.@this.NotFound(key);

        var prop = value.GetType().GetProperty(key,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
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
