namespace app.type.date;

public sealed partial class @this
{
    /// <summary>
    /// OBP: <c>date</c> owns how a date value is built. A <see cref="System.DateOnly"/>
    /// or <c>date.@this</c> passes through; a <see cref="System.DateTime"/>/
    /// <see cref="System.DateTimeOffset"/> projects to its date part; an ISO
    /// <c>yyyy-MM-dd</c> string parses. Output is the raw <c>DateOnly</c> the alias
    /// target expects.
    /// </summary>
    public static global::app.data.@this Convert(object? value, string? kind,
        global::app.actor.context.@this context)
    {
        switch (value)
        {
            case null: return global::app.data.@this.Ok(value);
            case System.DateOnly: return global::app.data.@this.Ok(value);
            case @this self: return global::app.data.@this.Ok(self.Value);
            case System.DateTime dt: return global::app.data.@this.Ok(System.DateOnly.FromDateTime(dt));
            case System.DateTimeOffset dto: return global::app.data.@this.Ok(System.DateOnly.FromDateTime(dto.DateTime));
            case string s when System.DateOnly.TryParse(s,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d):
                return global::app.data.@this.Ok(d);
            case string s:
                return global::app.data.@this.FromError(new global::app.error.Error(
                    $"Cannot parse '{s}' as date — expected ISO yyyy-MM-dd.", "DateParseFailed", 400));
            default:
                return global::app.data.@this.FromError(new global::app.error.Error(
                    $"Cannot convert {value.GetType().Name} to date.", "DateConversionFailed", 400));
        }
    }
}
