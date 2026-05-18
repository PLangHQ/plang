using System.Collections;

namespace app.variables.navigators;

/// <summary>
/// Navigates lists by numeric index and special accessors (first, last, random, count/length).
/// Supports implicit first-element delegation: %addresses.street% → addresses[0].street.
/// </summary>
public sealed class List : INavigator
{
    public bool CanNavigate(global::app.data.@this data)
        => data.Value is IList || IsGenericList(data.Value);

    public global::app.data.@this Navigate(global::app.data.@this data, string key)
    {
        var value = data.Value;
        var list = value as IList ?? WrapGenericList(value);
        if (list == null || list.Count == 0)
            return global::app.data.@this.NotFound(key);

        // Special accessors
        if (string.Equals(key, "count", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "length", StringComparison.OrdinalIgnoreCase))
            return new data.@this(key, list.Count, parent: data);

        if (string.Equals(key, "first", StringComparison.OrdinalIgnoreCase))
            return Element(list[0], key, data);

        if (string.Equals(key, "last", StringComparison.OrdinalIgnoreCase))
            return Element(list[list.Count - 1], key, data);

        if (string.Equals(key, "random", StringComparison.OrdinalIgnoreCase))
            return Element(list[Random.Shared.Next(list.Count)], key, data);

        // Index access
        if (int.TryParse(key, out var index))
        {
            if (index >= 0 && index < list.Count)
                return Element(list[index], key, data);
            return global::app.data.@this.NotFound(key);
        }

        // Implicit first: delegate to first element's navigator
        // e.g. %addresses.street% → addresses[0].street
        var firstElement = Element(list[0], "0", data);
        return ValueNavigators.Navigate(firstElement, key);
    }

    /// <summary>
    /// Returns a list element as Data. If the raw slot is already a Data (list.add stores
    /// the whole Data), return it as-is — don't double-wrap — so callers get the
    /// element's original type, context, and metadata intact.
    /// </summary>
    private static global::app.data.@this Element(object? raw, string key, global::app.data.@this parent)
    {
        if (raw is global::app.data.@this inner) return inner;
        return new data.@this(key, raw, parent: parent);
    }

    private static bool IsGenericList(object? value)
        => value?.GetType().GetInterfaces().Any(i =>
            i.IsGenericType && (
                i.GetGenericTypeDefinition() == typeof(IList<>) ||
                i.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))) ?? false;

    private static IList? WrapGenericList(object? value)
    {
        if (value == null) return null;
        var iface = value.GetType().GetInterfaces().FirstOrDefault(i =>
            i.IsGenericType && (
                i.GetGenericTypeDefinition() == typeof(IList<>) ||
                i.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)));
        if (iface == null) return null;

        var collectionIface = value.GetType().GetInterfaces().FirstOrDefault(i =>
            i.IsGenericType && (
                i.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                i.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>)));
        var count = (int)(collectionIface?.GetProperty("Count")?.GetValue(value) ?? 0);
        var indexer = iface.GetProperty("Item")!;
        var wrapper = new object?[count];
        for (int i = 0; i < count; i++)
            wrapper[i] = indexer.GetValue(value, [i]);
        return wrapper;
    }
}
