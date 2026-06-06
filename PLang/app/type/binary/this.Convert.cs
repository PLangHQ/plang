namespace app.type.binary;

public sealed partial class @this
{
    /// <summary>
    /// OBP: <c>binary</c> owns how a byte value is built — raw <c>byte[]</c> passes
    /// through; a base64 string decodes. Output is the raw <c>byte[]</c> the alias
    /// target expects.
    /// </summary>
    public static global::app.data.@this Convert(object? value, string? kind,
        global::app.actor.context.@this context)
    {
        switch (value)
        {
            case null: return global::app.data.@this.Ok(value);
            case byte[]: return global::app.data.@this.Ok(value);
            case @this self: return global::app.data.@this.Ok(self.Value);
            case string s:
                try { return global::app.data.@this.Ok(System.Convert.FromBase64String(s)); }
                catch (System.FormatException)
                {
                    return global::app.data.@this.FromError(new global::app.error.Error(
                        $"Cannot parse string as binary — expected base64.", "BinaryParseFailed", 400));
                }
            default:
                return global::app.data.@this.FromError(new global::app.error.Error(
                    $"Cannot convert {value.GetType().Name} to binary.", "BinaryConversionFailed", 400));
        }
    }
}
