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
        bool returnWrapper = string.IsNullOrEmpty(kind);
        global::app.data.@this B(byte[] bytes) => global::app.data.@this.Ok(returnWrapper ? (object?)(@this)bytes : bytes);
        switch (value)
        {
            case null: return global::app.data.@this.Ok(value);
            case byte[] b2: return B(b2);
            case @this self: return B(self.Value);
            case string s:
                try { return B(System.Convert.FromBase64String(s)); }
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
