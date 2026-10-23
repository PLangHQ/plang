namespace app.type.item.size.si;

/// <summary>A size in SI — powers of 1000: <c>512 kB</c>, <c>100 MB</c>, <c>2 GB</c>. <c>KB</c> and <c>mb</c> read here
/// too: their suffix is this standard's, whatever its case.</summary>
public sealed class @this : size.@this, global::app.type.item.ICreate<@this>
{
    private const long step = 1000;
    private static readonly string[] units = ["kB", "MB", "GB", "TB", "PB"];

    /// <summary>A count born holding <paramref name="type"/>, the app's <c>{size, si}</c>.</summary>
    public @this(long bytes, global::app.type.@this type) : base(bytes, type) { }

    /// <summary>Its text in SI — <c>100 MB</c>.</summary>
    public override System.Threading.Tasks.ValueTask<string> Text(global::app.actor.context.@this context) => new(Spell(step, units));

    public override string ToString() => Spell(step, units);

    /// <summary>A count, or a text in SI units, as an SI size born holding <paramref name="type"/>; a text in
    /// another standard is refused, naming how SI is written.</summary>
    public static new @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        if (type is not null && Count(raw, step, units) is { } count) return new(count, type);
        Refuse(raw, "si", "100 MB", data);
        return null;
    }
}
