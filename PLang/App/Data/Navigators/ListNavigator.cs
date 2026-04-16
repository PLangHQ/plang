using System.Collections;

namespace App.Data.Navigators;

/// <summary>
/// Navigates lists by numeric index and special accessors (first, last, random, count/length).
/// Supports implicit first-element delegation: %addresses.street% → addresses[0].street.
/// </summary>
public sealed class ListNavigator : INavigator
{
    public bool CanNavigate(Data.@this data)
        => data.Value is IList || IsGenericList(data.Value);

    public Data.@this Navigate(Data.@this data, string key)
    {
        var value = data.Value;
        var list = value as IList ?? WrapGenericList(value);
        if (list == null || list.Count == 0)
            return Data.@this.NotFound(key);

        // Special accessors
        if (string.Equals(key, "count", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "length", StringComparison.OrdinalIgnoreCase))
            return new Data.@this(key, list.Count, parent: data);

        if (string.Equals(key, "first", StringComparison.OrdinalIgnoreCase))
            return new Data.@this(key, list[0], parent: data);

        if (string.Equals(key, "last", StringComparison.OrdinalIgnoreCase))
            return new Data.@this(key, list[list.Count - 1], parent: data);

        if (string.Equals(key, "random", StringComparison.OrdinalIgnoreCase))
            return new Data.@this(key, list[Random.Shared.Next(list.Count)], parent: data);

        // Index access
        if (int.TryParse(key, out var index))
        {
            if (index >= 0 && index < list.Count)
                return new Data.@this(key, list[index], parent: data);
            return Data.@this.NotFound(key);
        }

        // Implicit first: delegate to first element's navigator
        // e.g. %addresses.street% → addresses[0].street
        var firstElement = new Data.@this("0", list[0], parent: data);
        return ValueNavigators.Navigate(firstElement, key);
    }

    private static bool IsGenericList(object? value)
        => value?.GetType().GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>)) ?? false;

    private static IList? WrapGenericList(object? value)
    {
        if (value == null) return null;
        var iface = value.GetType().GetInterfaces().FirstOrDefault(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
        if (iface == null) return null;

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
