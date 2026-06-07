namespace app.type.duration;

public sealed partial class @this : global::app.data.ITextCoercible
{
    /// <summary>Parse an ISO-8601 (<c>PT30S</c>) or .NET timespan (<c>00:00:30</c>) string into a
    /// duration so it reconciles with a duration operand in <c>==</c>/ordering. Mirrors
    /// <see cref="Resolve"/> (context-free here — the coercion mediator carries no context;
    /// reuses the same private <c>TryParseIso</c>).</summary>
    public object? CoerceText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();
        if (text.StartsWith('P') || (text.StartsWith('-') && text.Length > 1 && text[1] == 'P'))
        {
            var iso = TryParseIso(text);
            if (iso != null) return new @this(iso.Value);
        }
        return System.TimeSpan.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? new @this(v) : null;
    }
}
