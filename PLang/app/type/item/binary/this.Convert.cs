namespace app.type.item.binary;

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
        // Always born-native: binary builds a `binary` value. A .NET edge unwraps with .Clr<byte[]>().
        global::app.data.@this B(byte[] bytes) => context.Ok((@this)bytes);
        switch (value)
        {
            case null: return context.Ok(value);
            case byte[] b2: return B(b2);
            case @this self: return B(self.Value);
            case string s:
                try { return B(System.Convert.FromBase64String(s)); }
                catch (System.FormatException)
                {
                    return context.Error(new global::app.error.Error(
                        $"Cannot parse string as binary — expected base64.", "BinaryParseFailed", 400));
                }
            default:
                return context.Error(new global::app.error.Error(
                    $"Cannot convert {value.GetType().Name} to binary.", "BinaryConversionFailed", 400));
        }
    }
}
