namespace app.type.item.size.iec;

/// <summary>A size in IEC 80000-13 — powers of 1024: <c>500 KiB</c>, <c>95.4 MiB</c>, <c>2 GiB</c>.</summary>
public sealed class @this : size.@this, global::app.type.item.ICreate<@this>
{
    // its units above a byte, smallest first, and how many bytes one of the smallest is
    private static readonly string[] Units = ["KiB", "MiB", "GiB", "TiB", "PiB"];
    private const long Step = 1024;

    /// <summary>A count born holding <paramref name="type"/>, the app's <c>{size, iec}</c>.</summary>
    public @this(long bytes, global::app.type.@this type) : base(bytes, type) { }

    public override string Text => Form(Step, Units);

    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.String(Text);
        return default;
    }

    /// <summary>A count, or a text in IEC units, as an IEC size born holding <paramref name="declared"/>; a text in
    /// another standard declines (<c>null</c>).</summary>
    public static new @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
        => declared is { } type && (raw is global::app.type.item.@this leaf ? leaf.Clr<object>() : raw) switch
        {
            long bytes => bytes,
            int bytes => bytes,
            string text => Bytes(text, Step, Units),
            _ => null,
        } is { } count ? new(count, type) : null;
}
