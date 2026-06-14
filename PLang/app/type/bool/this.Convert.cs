namespace app.type.@bool;

public sealed partial class @this
{
    /// <summary>
    /// OBP: <c>bool</c> owns how a boolean value is built. A raw bool or a
    /// <c>bool.@this</c> passes through to the raw CLR <see cref="bool"/> the alias
    /// target expects; <c>"true"</c>/<c>"false"</c> (case-insensitive) parse.
    /// Output is the raw bool (born-native flips this to the wrapper later).
    /// </summary>
    public static global::app.data.@this Convert(object? value, string? kind,
        global::app.actor.context.@this context)
    {
        // Always born-native: bool builds a `bool` value. A .NET edge that needs the
        // raw CLR bool unwraps with .Clr<bool>().
        global::app.data.@this B(bool b) => global::app.data.@this.Ok((@this)b);
        switch (value)
        {
            case null: return global::app.data.@this.Ok(value);
            case bool b2: return B(b2);
            case @this self: return B(self.Value);
            case string s when bool.TryParse(s, out var b): return B(b);
            case string s:
                return global::app.data.@this.FromError(new global::app.error.Error(
                    $"Cannot parse '{s}' as bool — expected true or false.", "BoolParseFailed", 400));
            default:
                return global::app.data.@this.FromError(new global::app.error.Error(
                    $"Cannot convert {value.GetType().Name} to bool.", "BoolConversionFailed", 400));
        }
    }
}
