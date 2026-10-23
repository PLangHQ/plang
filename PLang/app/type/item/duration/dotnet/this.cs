namespace app.type.item.duration.dotnet;

/// <summary>A span in .NET's text — <c>00:00:30</c>, <c>1.02:03:04</c>. Always with its colons: a bare number is no
/// duration in any standard.</summary>
public sealed class @this : duration.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>A span born holding <paramref name="type"/>, the app's <c>{duration, dotnet}</c>.</summary>
    public @this(System.TimeSpan value, global::app.type.@this type) : base(value, type) { }

    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.String(Value.ToString("c", System.Globalization.CultureInfo.InvariantCulture));
        return default;
    }

    /// <summary>A span, or a text in .NET's form, as a dotnet duration born holding <paramref name="type"/>; a text
    /// in another form is refused, naming how dotnet is written.</summary>
    public static new @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        if (type is not null && Measure(raw, Read) is { } value) return new(value, type);
        Refuse(raw, "dotnet", "00:00:30", data);
        return null;

        static System.TimeSpan? Read(string text)
            => text.Contains(':') && System.TimeSpan.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var read)
                ? read : null;
    }
}
