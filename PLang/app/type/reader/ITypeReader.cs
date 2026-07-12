namespace app.type.reader;

/// <summary>
/// The type-owned, format-agnostic value read — the read-side mirror of a type's
/// <c>Write(value, IWriter)</c> renderer. The type <em>pulls</em> its own value
/// off an <see cref="app.channel.serializer.IReader"/> (<c>reader.Long()</c>,
/// <c>reader.BeginArray()</c>, …) and constructs itself, knowing only the abstract
/// reader — never the concrete format. The same impl serves every front-end: a
/// <c>json.Reader</c> over the <c>.pr</c> value token, a <c>csv.Reader</c> over a
/// content payload, a bytes reader over a binary blob.
///
/// <para>The read method is generic over the reader with the <c>allows ref struct</c>
/// anti-constraint so a stack-only <c>ref struct</c> reader (e.g.
/// <c>json.Reader</c> over <c>Utf8JsonReader</c>) crosses without boxing,
/// monomorphized per format at the call site. The reader is threaded <b>by ref</b>
/// so its single embedded cursor advances in place.</para>
/// </summary>
public interface ITypeReader
{
    /// <summary>
    /// The kind variant this reader handles within its type
    /// (<c>"json"</c>/<c>"csv"</c>/…), or <see cref="@this.AnyKind"/> (<c>"*"</c>)
    /// when the type reads uniformly across kinds. The parent folder names the
    /// PLang type; this names the kind — the (type, kind) registry key.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Pull this type's value off <paramref name="reader"/>, positioned at the
    /// value's first token, and return the born-native instance. The cursor is
    /// left on the value's last token (the <see cref="app.channel.serializer.IReader"/>
    /// contract). <paramref name="kind"/> is the concrete kind being read (the
    /// registry may have matched via the wildcard).
    /// </summary>
    global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind, ReadContext ctx)
        where TReader : app.channel.serializer.IReader, allows ref struct;

    /// <summary>
    /// True when a STRING token IS this type's own content — the value slot decodes it
    /// (text, datetime, guid, path, choice, url, csv text, base64 image; the polymorphic
    /// <c>object</c>/<c>item</c> apex too — a scalar can be declared object). Default true.
    /// A type whose canonical json form is NOT a string overrides FALSE (number, bool, dict,
    /// list): a string arriving under it is a mismatch, so it rides a strict wire and fails at
    /// first touch. Consulted at the <c>.pr</c> value mint, only when the token is a string: a
    /// content string borns a content source (raw = decoded content, so Peek / interpolation /
    /// events / display read the content, not a quoted document slice); everything else is a
    /// wire. The fact lives HERE — the type reader already owns "what a string means to me" —
    /// never as a face catalog above the types.
    /// </summary>
    bool StringIsContent => true;
}
