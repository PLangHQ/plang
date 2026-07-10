namespace app.type.duration;

public sealed partial class @this
{
    /// <summary>
    /// OBP: <c>duration</c> owns how a timespan value is built from a string —
    /// both CLR <see cref="System.TimeSpan"/> text (<c>"00:30:00"</c>) and ISO-8601
    /// duration (<c>"PT30S"</c>, <c>"P1DT2H"</c>) parse (see <see cref="Resolve"/>).
    /// Output is the raw <see cref="System.TimeSpan"/> the alias target expects.
    /// </summary>
    public static global::app.data.@this Convert(object? value, string? kind,
        global::app.actor.context.@this context)
    {
        // Born-native: a duration literal arrives as text — unwrap so the string
        // parse below sees the ISO/timespan text instead of the wrapper.
        if (value is global::app.type.item.text.@this txt) value = txt.Clr<string>();
        // Always born-native: duration builds a `duration` value. A .NET edge unwraps with .Clr<TimeSpan>().
        global::app.data.@this D(System.TimeSpan t) => context.Ok((@this)t);
        switch (value)
        {
            case null: return context.Ok(value);
        case System.TimeSpan ts2: return D(ts2);
        case @this self: return D(self.Value);
        case string s:
            var parsed = Resolve(s, context);
            if (parsed != null) return D(parsed.Value);
                return context.Error(new global::app.error.Error(
                    $"Cannot parse '{s}' as duration — expected ISO-8601 (e.g. PT30S) or .NET format (e.g. 00:00:30).",
                    "DurationParseFailed", 400));
            default:
                return context.Error(new global::app.error.Error(
                    $"Cannot convert {value.GetType().Name} to duration.", "DurationConversionFailed", 400));
        }
    }
}
