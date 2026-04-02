using System.Collections;

namespace PLang.Runtime2.Engine.Memory.Navigators;

/// <summary>
/// Navigates IList by numeric index, named accessors (first/last/random),
/// or implicit first element delegation.
/// </summary>
public class ListNavigator : IValueNavigator
{
    public bool CanNavigate(object value)
        => value is IList || IsGenericList(value);

    public object? GetProperty(object value, string key)
    {
        var list = value as IList ?? WrapGenericList(value);
        if (list == null || list.Count == 0)
            return null;

        // Numeric index
        if (int.TryParse(key, out var index))
        {
            return index >= 0 && index < list.Count ? list[index] : null;
        }

        // Named accessors
        if (string.Equals(key, "first", StringComparison.OrdinalIgnoreCase))
            return list[0];

        if (string.Equals(key, "last", StringComparison.OrdinalIgnoreCase))
            return list[list.Count - 1];

        if (string.Equals(key, "random", StringComparison.OrdinalIgnoreCase))
            return list[Random.Shared.Next(list.Count)];

        if (string.Equals(key, "count", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "length", StringComparison.OrdinalIgnoreCase))
            return list.Count;

        // Implicit first: delegate to first element's navigator
        // e.g. %addresses.street% → addresses[0].street
        return ValueNavigators.Navigate(list[0]!, key);
    }

    private static bool IsGenericList(object value)
        => value.GetType().GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));

    private static IList? WrapGenericList(object value)
    {
        var iface = value.GetType().GetInterfaces().FirstOrDefault(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
        if (iface == null) return null;

        // Count is on ICollection<T>, not IList<T> directly
        var collectionIface = value.GetType().GetInterfaces().FirstOrDefault(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>));
        var count = (int)(collectionIface?.GetProperty("Count")?.GetValue(value) ?? 0);
        var indexer = iface.GetProperty("Item")!;
        var wrapper = new object?[count];
        for (int i = 0; i < count; i++)
            wrapper[i] = indexer.GetValue(value, [i]);
        return wrapper;
    }
}
