namespace PLang.Tests;

/// <summary>
/// Test-only typed read — the old <c>Data.GetValue&lt;T&gt;</c> surface
/// (in-memory form + the one converter, default on failure). Production code
/// reads through the value door and the item's own lowering; tests asserting
/// over already-resolved values keep this ergonomic shim.
/// </summary>
public static class DataReadExtensions
{
    public static T? GetValue<T>(this global::app.data.@this d)
        => d.GetValue(typeof(T)) is T result ? result : default;

    public static object? GetValue(this global::app.data.@this d, System.Type targetType)
    {
        var v = d.Peek();
        if (v == null) return null;
        if (targetType.IsInstanceOfType(v)) return v;
        var (converted, _) = global::app.type.catalog.@this.TryConvert(v, targetType);
        return converted;
    }

    /// <summary>
    /// The RESOLVED typed read — opens the value door, then converts. Unlike
    /// <see cref="GetValue{T}"/> (which reads the in-memory Peek), this resolves
    /// templates / %ref% / deep container holes first. A stamped container is
    /// non-cacheable, so the resolved form is the door's RETURN, not what Peek
    /// would show — use this whenever the value under test carries variables.
    /// </summary>
    public static async System.Threading.Tasks.Task<T?> ResolvedValue<T>(this global::app.data.@this d)
    {
        var v = await d.Value();
        if (v == null) return default;
        if (v is T typed) return typed;
        var (converted, _) = global::app.type.catalog.@this.TryConvert(v, typeof(T));
        return converted is T result ? result : default;
    }
}
