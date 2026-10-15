namespace app.type.kind;

/// <summary>
/// A kind of a parent type <typeparamref name="T"/> (decision 774): <c>decimal</c> is a
/// <c>kind&lt;number&gt;</c>. A kind IS a type (<c>kind : type</c>); this layer names the parent the
/// kind sits under, and is the one seam where a kind that does not answer a type question itself
/// passes it to <typeparamref name="T"/>.
///
/// <para>A concrete kind overrides only what is its OWN — how its value parses, the token it reads
/// and writes. Everything else reads as the parent's: a number value never routes an operation
/// through its kind (it asks only <c>Create</c>/<c>Write</c>), and a kinded type's facts already
/// resolve to the parent through <c>type</c>'s own kind→Family path, so for number nothing needs an
/// explicit forward here. The seam carries <typeparamref name="T"/> for the families that will
/// (stage 3).</para>
/// </summary>
public abstract class @this<T> : @this where T : global::app.type.item.@this
{
    // Family = the parent type (by T's plang name), so a kind answers its own, else its parent's: a
    // decimal kind's teaching (Description/Example through Taught(Family.Name)) and its stored facts
    // fall back to number's. Ruling 3, wired here once for every kind of T.
    protected @this(string name)
        : base(name, new global::app.type.@this(global::app.type.item.@this.NameOf(typeof(T)))) { }

    // A format of T declared on T's class with [Format] (image's png, text's md): the MIMEs, extension,
    // and T's own encode/decode. Owner is T below, so no owner string is carried.
    protected @this(global::app.Attributes.FormatAttribute format,
                    global::app.type.kind.@this.Encoder? encode, global::app.type.kind.@this.Decoder? decode)
        : base(format, global::app.type.item.@this.NameOf(typeof(T)), encode, decode) { }

    // The parent T names the owner, so a kind of T needs no string Owner of its own (decision 774):
    // its content is a T, and the type registry holds it under T by this.
    protected internal override string? Owner => global::app.type.item.@this.NameOf(typeof(T));
}
