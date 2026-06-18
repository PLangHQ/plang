namespace app.type.text.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.text.@this"/> — the type reads its own value off the single
/// decode pass. Borns the text directly from the string token, the same shape as
/// <see cref="Default.Read"/>: <c>canTemplate</c> so text itself decides whether
/// the raw carries a <c>%ref%</c> template (<see cref="app.type.text.@this.HasHoles"/>),
/// resolution staying lazy at the door.
///
/// <para>The authored-vs-literal <c>Template</c> stamp is NOT set here — it rides
/// the reader's mode under the (separate) template-stamping-at-read design; until
/// that lands the post-parse seam stamps it, exactly as for the eager path.</para>
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
        => reader.Null()
            ? new global::app.type.@null.@this("text", kind)
            : new global::app.type.text.@this(reader.String(), canTemplate: true) { Kind = kind };
}
