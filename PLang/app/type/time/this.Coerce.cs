namespace app.type.time;

public sealed partial class @this : global::app.data.ITextCoercible
{
    /// <summary>Parse an ISO <c>HH:mm[:ss]</c> string into a time so it reconciles with a
    /// time operand in <c>==</c>/ordering. Mirrors the string arm of <see cref="Convert"/>.</summary>
    public object? CoerceText(string text) =>
        System.TimeOnly.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var t) ? new @this(t) : null;
}
