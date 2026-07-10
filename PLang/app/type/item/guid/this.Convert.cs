namespace app.type.item.guid;

public sealed partial class @this
{
    /// <summary>
    /// OBP: <c>guid</c> owns how a guid value is built. A CLR <see cref="System.Guid"/>
    /// passes straight through; a string parses via <see cref="Resolve"/> (canonical,
    /// braced, or hyphenless). Output is the raw <see cref="System.Guid"/> the alias
    /// target expects.
    /// </summary>
    public static global::app.data.@this Convert(object? value, string? kind,
        global::app.actor.context.@this context)
    {
        // Born-native: a guid literal arrives as text — unwrap so the string
        // parse below sees the guid text instead of the wrapper.
        if (value is global::app.type.item.text.@this txt) value = txt.Clr<string>();
        // Always born-native: guid builds a `guid` value. A .NET edge unwraps with .Clr<Guid>().
        global::app.data.@this G(System.Guid g) => context.Ok(new @this(g));
        switch (value)
        {
            case null: return context.Ok(value);
            case System.Guid raw: return G(raw);
            case @this self: return G(self.Value);
            case string s:
                var parsed = Resolve(s, context);
                if (parsed != null) return G(parsed.Value);
                return context.Error(new global::app.error.Error(
                    $"Cannot parse '{s}' as guid — expected a 36-char guid (e.g. 550e8400-e29b-41d4-a716-446655440000).",
                    "GuidParseFailed", 400));
            default:
                return context.Error(new global::app.error.Error(
                    $"Cannot convert {value.GetType().Name} to guid.", "GuidConversionFailed", 400));
        }
    }
}
