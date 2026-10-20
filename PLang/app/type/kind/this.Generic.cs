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
    // The owner is the app's entry for T, handed in by the registry that enlists the kind (T names which entry);
    // it is the kind's Family, so a kind answers its own, else its parent's: a decimal kind's teaching
    // (Description/Example through Taught(Family.Name)) and its stored facts fall back to number's.
    protected @this(string name, global::app.type.@this owner) : base(name, owner) { }

    // A kind of T whose own kind is nested (a list of lists: {list, kind: {list, kind: path}}) — the nested
    // kind rides as this kind's own, T still the parent. Null nested is the plain kind<T>.
    private protected @this(string name, global::app.type.kind.@this? nested, global::app.type.@this owner)
        : base(name, nested, owner) { }

    // A format of T declared on T's class with [Format] (image's png, text's md): the MIMEs and extension.
    protected @this(global::app.Attributes.FormatAttribute format, global::app.type.@this owner)
        : base(format, owner) { }
}
