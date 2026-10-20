namespace app.type.item.size.iec;

/// <summary>A size in IEC 80000-13 — powers of 1024: <c>500 KiB</c>, <c>95.4 MiB</c>, <c>2 GiB</c>.</summary>
public sealed class @this : size.@this, global::app.type.item.ICreate<@this>
{
    private const long step = 1024;
    private static readonly string[] units = ["KiB", "MiB", "GiB", "TiB", "PiB"];

    /// <summary>A count born holding <paramref name="type"/>, the app's <c>{size, iec}</c>.</summary>
    public @this(long bytes, global::app.type.@this type) : base(bytes, type) { }

    protected override long Step => step;
    protected override string[] Units => units;

    /// <summary>A count, or a text in IEC units, as an IEC size born holding <paramref name="declared"/>; a text in
    /// another standard is refused, naming how IEC is written.</summary>
    public static new @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (declared is { } type && Bytes(raw, step, units) is { } count) return new(count, type);
        Refuse(raw, "iec", "95.4 MiB", data);
        return null;
    }
}
