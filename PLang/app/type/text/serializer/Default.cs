namespace app.type.text.serializer;

/// <summary>
/// Reader for <see cref="app.type.text.@this"/> — the Data-free
/// <c>raw → text</c> deserialize the reader registry dispatches for the
/// <c>text</c> type. Format-agnostic: the injected serializer turns wire bytes
/// into the CLR <paramref name="raw"/> (a string), and this turns that into the
/// type instance, the same for every format.
///
/// <para>A value slot of <c>text</c> permits a <c>%var%</c> reference, so the
/// text is built with <c>canTemplate:true</c> — text itself decides whether the
/// raw actually carries a template (<see cref="app.type.text.@this.HasHoles"/>);
/// resolution stays lazy at the door. No <c>Write</c> here — text renders through
/// its own <see cref="app.type.text.@this.Write"/> / json converter; this file
/// only adds the read side.</para>
/// </summary>
public static class Default
{
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
        => raw switch
        {
            null => null,
            global::app.type.text.@this t => t,
            // A string is the value; binary bytes off I/O decode in the text ctor
            // (the text class owns bytes→string — born only from a decoded string).
            string or byte[] => new global::app.type.text.@this(raw, ctx.Template) { Kind = kind },
            _ => new global::app.type.text.@this(raw.ToString() ?? "", ctx.Template) { Kind = kind },
        };
}
