using System.Reflection;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine.Navigators;

/// <summary>
/// Fallback navigator — uses CLR reflection to navigate public properties on any object.
/// </summary>
public sealed class ClrReflectionNavigator : INavigator
{
    public object? Navigate(Data data, string key)
    {
        var value = data.Value;
        if (value == null) return null;

        var prop = value.GetType().GetProperty(key,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return prop?.GetValue(value);
    }
}
