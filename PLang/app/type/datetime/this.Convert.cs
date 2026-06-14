namespace app.type.datetime;

public sealed partial class @this
{
    /// <summary>
    /// OBP: <c>datetime</c> owns how a date/time value is built from a string.
    /// The ISO-8601-with-timezone wire form is canonical (see <see cref="Resolve"/>);
    /// output is the raw <see cref="System.DateTimeOffset"/> the alias target expects.
    /// A value already in date/time shape passes straight through.
    /// </summary>
    public static global::app.data.@this Convert(object? value, string? kind,
        global::app.actor.context.@this context)
    {
        // Always born-native: datetime builds a `datetime` value. A .NET edge unwraps with .Clr<DateTimeOffset>().
        global::app.data.@this B(System.DateTimeOffset v) => global::app.data.@this.Ok(new @this(v));
        switch (value)
        {
            case null: return global::app.data.@this.Ok(value);
            case System.DateTimeOffset dto: return B(dto);
            case System.DateTime dt: return B(new System.DateTimeOffset(dt));
            case @this self: return B(self.Value);
            case string s:
                var parsed = Resolve(s, context);
                if (parsed != null) return B(parsed.Value);
                return global::app.data.@this.FromError(new global::app.error.Error(
                    $"Cannot parse '{s}' as datetime — expected ISO-8601 (e.g. 2024-03-15T10:30:00+00:00).",
                    "DateTimeParseFailed", 400));
            default:
                return global::app.data.@this.FromError(new global::app.error.Error(
                    $"Cannot convert {value.GetType().Name} to datetime.", "DateTimeConversionFailed", 400));
        }
    }
}
