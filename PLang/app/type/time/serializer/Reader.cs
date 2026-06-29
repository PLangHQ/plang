namespace app.type.time.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.time.@this"/> — the type reads its own value off the
/// single decode pass. The writer emits a String token
/// (<c>time.Write</c> → <c>w.String(ToString())</c>), so the reader pulls the
/// string and borns the <see cref="System.TimeOnly"/>. A malformed string throws
/// <see cref="System.FormatException"/>, which <c>source.Value</c> turns into the
/// binding-named <c>MaterializeFailed</c>.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
        => reader.Null()
            ? new global::app.type.@null.@this("time", kind)
            : new global::app.type.time.@this(System.TimeOnly.Parse(
                reader.String(), System.Globalization.CultureInfo.InvariantCulture));
}
