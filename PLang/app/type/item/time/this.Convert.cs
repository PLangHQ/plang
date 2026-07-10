namespace app.type.item.time;

public sealed partial class @this
{
    /// <summary>
    /// OBP: <c>time</c> owns how a time-of-day value is built. A
    /// <see cref="System.TimeOnly"/> or <c>time.@this</c> passes through; a
    /// <see cref="System.DateTime"/>/<see cref="System.DateTimeOffset"/> projects
    /// to its time part; an ISO <c>HH:mm[:ss]</c> string parses. Output is the raw
    /// <c>TimeOnly</c> the alias target expects.
    /// </summary>
    public static global::app.data.@this Convert(object? value, string? kind,
        global::app.actor.context.@this context)
    {
        // Always born-native: time builds a `time` value. A .NET edge unwraps with .Clr<TimeOnly>().
        global::app.data.@this B(System.TimeOnly v) => context.Ok(new @this(v));
        switch (value)
        {
            case null: return context.Ok(value);
            case System.TimeOnly t0: return B(t0);
            case @this self: return B(self.Value);
            case System.DateTime dt: return B(System.TimeOnly.FromDateTime(dt));
            case System.DateTimeOffset dto: return B(System.TimeOnly.FromDateTime(dto.DateTime));
            case string s when System.TimeOnly.TryParse(s,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var t):
                return B(t);
            case string s:
                return context.Error(new global::app.error.Error(
                    $"Cannot parse '{s}' as time — expected ISO HH:mm:ss.", "TimeParseFailed", 400));
            default:
                return context.Error(new global::app.error.Error(
                    $"Cannot convert {value.GetType().Name} to time.", "TimeConversionFailed", 400));
        }
    }
}
