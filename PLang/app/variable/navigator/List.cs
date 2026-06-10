using System.Collections;

namespace app.variable.navigator;

/// <summary>
/// Navigates lists by numeric index and special accessors (first, last, random, count/length).
/// Supports implicit first-element delegation: %addresses.street% → addresses[0].street.
/// </summary>
public sealed class List : INavigator
{
    public bool CanNavigate(global::app.data.@this data)
    {
        var v = data.Peek();
        return v is app.type.list.@this || v is IList || IsGenericList(v);
    }

    public async System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(global::app.data.@this data, string key)
    {
        var value = await data.Value();
        // The native `list` value type owns index/accessor navigation — its elements
        // are already Data, so they return directly (no WrapItem). Symmetric to dict.
        if (value is app.type.list.@this nativeList)
            return await NavigateNative(nativeList, key, data);

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
        return await ValueNavigators.Navigate(firstElement, key);
    }

    /// <summary>
    /// Navigation on the native list value type. Intrinsics (count/length, first, last,
    /// random, index) win; any other key falls through to the implicit-first element
    /// (`%addresses.street%` → `addresses[0].street`). Every element IS a Data already,
    /// so it returns directly — no raw-vs-Data recognition, no WrapItem.
    /// </summary>
    private static async System.Threading.Tasks.ValueTask<global::app.data.@this> NavigateNative(app.type.list.@this list, string key, global::app.data.@this parent)
    {
        if (string.Equals(key, "count", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "length", StringComparison.OrdinalIgnoreCase))
            return new data.@this(key, list.Count, parent: parent);

        if (list.Count == 0) return global::app.data.@this.NotFound(key);

        if (string.Equals(key, "first", StringComparison.OrdinalIgnoreCase))
            return list.First!;
        if (string.Equals(key, "last", StringComparison.OrdinalIgnoreCase))
            return list.Last!;
        if (string.Equals(key, "random", StringComparison.OrdinalIgnoreCase))
            return list.At(Random.Shared.Next(list.CountRaw))!;

        if (int.TryParse(key, out var index))
            return list.At(index) ?? global::app.data.@this.NotFound(key);

        // Implicit first: delegate to the first element's navigator.
        return await ValueNavigators.Navigate(list.First!, key);
    }

    /// <summary>
    /// Returns a raw list element as Data — fallback for genuine raw IList values
    /// (infra collections). A native `list`'s elements never reach here. An element
    /// already a Data returns as-is; a bare value is wrapped.
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
