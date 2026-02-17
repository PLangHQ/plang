using System.Reflection;

namespace PLang.Runtime2.Engine.Memory.Navigators;

/// <summary>
/// Navigates CLR objects via reflection. Lowest priority fallback.
/// </summary>
public class ObjectNavigator : IValueNavigator
{
    public bool CanNavigate(object value) => true;

    public object? GetProperty(object value, string key)
    {
        var prop = value.GetType().GetProperty(key,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        return prop?.GetValue(value);
    }
}
