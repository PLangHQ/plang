namespace app.type.item.datetime;

/// <summary>
/// String → datetime. ISO-8601 with timezone is the canonical wire form;
/// <see cref="System.DateTimeOffset.TryParse(string, out System.DateTimeOffset)"/>
/// accepts every shape PLang exposes (round-trip "o" format, "yyyy-MM-dd
/// HH:mm:ss", etc.).
/// </summary>
public sealed partial class @this
{
    public static @this? Resolve(string raw, global::app.actor.context.@this context)
        => Parse(raw, context.App.type.list[typeof(@this)]);

    /// <summary>The datetime <paramref name="raw"/> spells, born holding <paramref name="type"/>; null when it spells
    /// none.</summary>
    internal static @this? Parse(string raw, global::app.type.@this type)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return System.DateTimeOffset.TryParse(raw,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var v)
            ? new @this(v, type) : null;
    }
}
