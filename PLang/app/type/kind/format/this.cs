namespace app.type.kind.format;

/// <summary>
/// The kind a <c>[Format]</c> declaration mints — one concrete <c>kind&lt;T&gt;</c> for every format a type's
/// class declares (image's png/gif/…, text's md/xml, table's csv), T the declaring type. One class, many
/// instances (as a choice set is one class over many enums): a format differs only in data — its MIMEs, its
/// extension, whether it compresses, and the type's own encode/decode — so it needs no class of its own. The
/// registry makes one per <c>[Format]</c> (decision 774).
/// </summary>
public sealed class @this<T> : global::app.type.kind.@this<T> where T : global::app.type.item.@this
{
    public @this(global::app.Attributes.FormatAttribute format,
                 global::app.type.kind.@this.Encoder? encode = null, global::app.type.kind.@this.Decoder? decode = null)
        : base(format, encode, decode) { }
}
