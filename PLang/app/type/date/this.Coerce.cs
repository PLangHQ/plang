namespace app.type.date;

public sealed partial class @this : global::app.data.ITextCoercible
{
    /// <summary>Parse an ISO <c>yyyy-MM-dd</c> string into a date so it reconciles with a
    /// date operand in <c>==</c>/ordering. Mirrors the string arm of <see cref="Convert"/>.</summary>
    public object? CoerceText(string text) =>
        System.DateOnly.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d) ? new @this(d) : null;
}
