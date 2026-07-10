namespace app.type.item.datetime.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.item.datetime.@this"/> — the type reads its own value off the
/// single decode pass. The writer emits a <c>DateTimeOffset</c> token
/// (<c>datetime.Write</c> → <c>w.DateTimeOffset</c>), so the value borns from it
/// directly: <c>new datetime(reader.DateTimeOffset())</c>. A malformed token throws
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
            ? new global::app.type.item.@null.@this("datetime", kind)
            : new global::app.type.item.datetime.@this(reader.DateTimeOffset());
}
