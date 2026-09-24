namespace PLang.Tests.Shared;

/// <summary>Test reads over an error's variables snapshot — a dict of each variable's Data, keyed by
/// name. Tests ask whether a variable was captured and what value it held.</summary>
public static class VariableSnapshotExtensions
{
    /// <summary>True when the snapshot holds a variable named <paramref name="name"/>.</summary>
    public static bool Has(this global::app.type.item.dict.@this snapshot, string name)
        => snapshot.KeyNames.Any(k => string.Equals(k, name, System.StringComparison.OrdinalIgnoreCase));

    /// <summary>The value the variable held when captured, or null when it was not captured.</summary>
    public static object? Held(this global::app.type.item.dict.@this snapshot, string name)
    {
        var key = snapshot.KeyNames.FirstOrDefault(k => string.Equals(k, name, System.StringComparison.OrdinalIgnoreCase));
        if (key == null) return null;
        return snapshot.Stored(key) is global::app.data.@this variable ? variable.Peek() : snapshot.Stored(key);
    }
}
