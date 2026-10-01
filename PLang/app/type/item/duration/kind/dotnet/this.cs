namespace app.type.item.duration.kind.dotnet;

/// <summary>.NET's span text — <c>00:00:30</c>, <c>1.02:03:04</c>. Always with its colons: a bare number is no
/// duration in any standard.</summary>
public sealed class @this : kind.@this
{
    public @this() : base("dotnet") { }

    internal override System.TimeSpan? Span(string text)
        => text.Contains(':') && System.TimeSpan.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var span)
            ? span : null;

    internal override string Text(System.TimeSpan span) => span.ToString("c", System.Globalization.CultureInfo.InvariantCulture);
}
