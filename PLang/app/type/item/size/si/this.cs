namespace app.type.item.size.si;

/// <summary>A size in SI — powers of 1000: <c>512 kB</c>, <c>100 MB</c>, <c>2 GB</c>. <c>KB</c> and <c>mb</c> read here
/// too: their suffix is this standard's, whatever its case.</summary>
public sealed class @this : size.@this, global::app.type.item.ICreate<@this>
{
    private const long step = 1000;
    private static readonly string[] units = ["kB", "MB", "GB", "TB", "PB"];

    /// <summary>A count born holding <paramref name="type"/>, the app's <c>{size, si}</c>.</summary>
    public @this(long bytes, global::app.type.@this type) : base(bytes, type) { }

    protected override long Step => step;
    protected override string[] Units => units;

    /// <summary>A count, or a text in SI units, as an SI size born holding <paramref name="declared"/>; a text in
    /// another standard declines (<c>null</c>).</summary>
    public static new @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
        => declared is { } type && Bytes(raw, step, units) is { } count ? new(count, type) : null;
}
