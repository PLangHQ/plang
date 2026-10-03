namespace app.type.item.culture;

/// <summary>
/// PLang <c>culture</c> value — how numbers (and later dates) read as text for a people: <c>"is-IS"</c>,
/// <c>"en-US"</c>. Made from its name; a name no culture has is refused where it is given
/// (<c>set %!app.setting.culture% = "xx-YY"</c> fails at the set). Written as its name.
/// </summary>
[global::app.Attributes.PlangType("culture")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Shape => "string";

    private readonly System.Globalization.CultureInfo _info;

    private @this(System.Globalization.CultureInfo info) => _info = info;

    /// <summary>The machine's culture — what the app reads numbers as text by when none is set.</summary>
    public static @this Machine => new(System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>Its name, <c>is-IS</c>.</summary>
    public string Name => _info.Name;

    /// <summary>How it writes numbers — the decimals it shows and its separator.</summary>
    internal System.Globalization.NumberFormatInfo Number => _info.NumberFormat;

    protected internal override global::app.type.@this Type => new(typeof(@this));

    public override bool IsLeaf => true;

    /// <summary>The culture a name names, or null when no culture has that name.</summary>
    public static @this? Create(object? raw)
    {
        if (raw is @this self) return self;
        var name = raw is global::app.type.item.@this item ? item.Clr<object>()?.ToString() : raw?.ToString();
        if (name == null) return null;
        try { return new(System.Globalization.CultureInfo.GetCultureInfo(name, predefinedOnly: true)); }
        catch (System.Globalization.CultureNotFoundException) { return null; }
    }

    /// <summary>The culture a value names; a name no culture has is refused on <paramref name="data"/>.</summary>
    public static @this? Create(object? value, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (Create(value) is { } culture) return culture;
        data.Fail(new global::app.error.Error(
            $"'{(value as global::app.type.item.@this)?.Clr<object>() ?? value}' is no culture — a culture is named as is-IS or en-US.",
            "UnknownCulture", 400));
        return null;
    }

    public override void Write(global::app.type.format.IWriter w) => w.String(Name);

    public override string ToString() => Name;
}
