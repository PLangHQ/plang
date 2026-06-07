namespace app.type.datetime;

public sealed partial class @this : global::app.data.ITextCoercible
{
    /// <summary>Parse an ISO-8601 string into a datetime so it reconciles with a datetime
    /// operand in <c>==</c>/ordering. Mirrors <see cref="Resolve"/> (context-free here — the
    /// coercion mediator carries no context).</summary>
    public object? CoerceText(string text) =>
        System.DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var v) ? new @this(v) : null;
}
