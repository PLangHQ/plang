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
        switch (value)
        {
            case null: return global::app.data.@this.Ok(value);
            case bool: return global::app.data.@this.Ok(value);
            case @this self: return global::app.data.@this.Ok(self.Value);
            case string s when bool.TryParse(s, out var b): return global::app.data.@this.Ok(b);
            case string s:
                return global::app.data.@this.FromError(new global::app.error.Error(
                    $"Cannot parse '{s}' as bool — expected true or false.", "BoolParseFailed", 400));
            default:
                return global::app.data.@this.FromError(new global::app.error.Error(
                    $"Cannot convert {value.GetType().Name} to bool.", "BoolConversionFailed", 400));
        }
    }
}
