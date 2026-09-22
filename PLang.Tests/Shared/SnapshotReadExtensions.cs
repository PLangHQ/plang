namespace PLang.Tests.Shared;

/// <summary>
/// Reads snapshot entries in tests. Production <c>Restore</c> reads entries through the value's own
/// typed ask and converts at the use; these wrap that one-liner so an assertion stays one line.
/// Nothing here lowers to CLR beyond the conversion the assertion itself needs.
/// </summary>
public static class SnapshotReadExtensions
{
    /// <summary>True if an entry with this key was written.</summary>
    public static bool Has(this Snapshot s, string key) => s.Entries.Has(key);

    /// <summary>A text entry as a string. Null when the entry is absent.</summary>
    public static async Task<string?> Text(this Snapshot s, string key)
        => s.Entries.Get(key) is { } d
            ? (await d.Value<global::app.type.item.text.@this>()).ToString()
            : null;

    /// <summary>A number entry as an int.</summary>
    public static async Task<int> Int(this Snapshot s, string key)
        => (await s.Entries.Get(key)!.Value<global::app.type.item.number.@this>()).ToInt32();

    /// <summary>A list entry as its rows. Empty when the entry is absent.</summary>
    public static async Task<IReadOnlyList<global::app.data.@this>> Rows(this Snapshot s, string key)
        => s.Entries.Get(key) is { } d
            ? (await d.Value<global::app.type.item.list.@this>()).Items
            : Array.Empty<global::app.data.@this>();

    /// <summary>The rows of a list entry lowered to a CLR record — the same boundary Providers'
    /// own Restore crosses for Registration/DefaultOverride, which ride as clr carriers in-process.</summary>
    public static async Task<List<T>> Records<T>(this Snapshot s, string key)
    {
        var records = new List<T>();
        foreach (var row in await s.Rows(key))
            if (Lower<T>(row.Peek()) is { } record) records.Add(record);
        return records;
    }

    /// <summary>The rows of a list entry whose elements are themselves snapshots (e.g. frames).</summary>
    public static async Task<List<Snapshot>> Frames(this Snapshot s, string key)
    {
        var frames = new List<Snapshot>();
        foreach (var row in await s.Rows(key)) frames.Add(await row.Value<Snapshot>());
        return frames;
    }
}
