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

    /// <summary>A span, or a text in .NET's form, as a dotnet duration born holding <paramref name="declared"/>; a text
    /// in another form declines (<c>null</c>).</summary>
    public static new @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
    {
        return declared is { } type && Span(raw, Read) is { } value ? new(value, type) : null;

        static System.TimeSpan? Read(string text)
            => text.Contains(':') && System.TimeSpan.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var read)
                ? read : null;
    }
}
