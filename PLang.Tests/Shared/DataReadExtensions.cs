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
        // The value lowers ITSELF to the CLR target (the central convert door is gone).
        return v is global::app.type.item.@this it ? it.Clr(targetType) : v;
    }
}
