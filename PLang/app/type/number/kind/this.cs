namespace app.type.number.kind;

/// <summary>
/// A number STORAGE kind — one of the 15 sizes a number can be held in (int, long, decimal, …).
/// The kind owns how to CREATE a number of its size from a plang value, and how to READ/WRITE one on
/// the wire. It is <b>context-free, stateless behavior</b> — "how int serializes" has nothing per-App
/// about it — so a number VALUE carries its kind instance directly (no registry, no App):
/// <c>value.Kind.Write(value, writer)</c>, arithmetic rebuilds via the kind it picked, zero lookups at
/// the context-less sites. The 15 singletons live in a private map inside <c>number</c>.
///
/// <para>The cross-kind RELATION (which size an overflow climbs to) is NOT here — that's the Ladder's,
/// in <c>number/this.Ladder.cs</c>. Most kinds build by coercing the value's backing to their
/// <see cref="ClrForm"/> (the base default — <c>ChangeType</c> is the one internal .NET boundary);
/// the four CLR can't reach (BigInteger/Int128/UInt128/Half) override <see cref="Create"/>.</para>
/// </summary>
public abstract class @this
{
    /// <summary>The kind's name token ("int", "long", …) — its identity (equality is by name).</summary>
    public abstract string Name { get; }

    /// <summary>The CLR storage type of this kind (<c>typeof(int)</c>, …).</summary>
    public abstract System.Type ClrForm { get; }

    /// <summary>Build a number of this storage size from a plang value — plang in, plang out. The
    /// value lowers ITSELF to <see cref="ClrForm"/> through its own <c>Clr</c> door (a number by the
    /// time it reaches a kind — <c>number.Create</c> parses any string first — so this is just the
    /// number's own <c>ToInt32</c>/…); the kind reborns it at its size. <c>null</c> = can't be this kind.</summary>
    public virtual global::app.type.number.@this? Create(global::app.type.item.@this value)
        => global::app.type.number.@this.FromObject(value.Clr(ClrForm));

    /// <summary>Write a number of this kind to the wire — the kind emits its own token (a native
    /// numeric primitive, or the lossless invariant string for Int128/UInt128/BigInteger).</summary>
    public abstract void Write(global::app.type.number.@this value, global::app.channel.serializer.IWriter writer);

    /// <summary>Read a number of this kind off the wire — the inverse of <see cref="Write"/>.</summary>
    public abstract global::app.type.item.@this Read<TReader>(ref TReader reader)
        where TReader : global::app.channel.serializer.IReader, allows ref struct;

    public override string ToString() => Name;
}
