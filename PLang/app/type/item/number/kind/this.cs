namespace app.type.item.number.kind;

/// <summary>
/// A number STORAGE kind — one of the 15 sizes a number is held in (int, long, decimal, …). The kind
/// owns how to CREATE a number of its size from a plang value (plang in, plang out) and how to
/// READ/WRITE one on the wire. It is <b>context-free, stateless behavior</b> — a number VALUE carries
/// its kind instance directly (no registry, no App, no lookup at birth): <c>value.Kind.Write(…)</c>,
/// arithmetic rebuilds via the kind it picked.
///
/// <para>The cross-kind RELATION (which size an overflow climbs to) is NOT here — that's the Ladder's,
/// in <c>number/this.Ladder.cs</c>. <see cref="Create"/> has no default: the value lowers ITSELF to
/// the kind's storage through its own <c>Clr</c> door, and number's implicit operators lift it back —
/// so each kind is one line (the four CLR can't reach own their parse arms and throw precise). The
/// courier is the only caller that owns the error channel (it turns a thrown reason into data.Fail).</para>
/// </summary>
public abstract class @this
{
    /// <summary>The kind's name token ("int", "long", …) — its identity (equality is by name).</summary>
    public abstract string Name { get; }

    /// <summary>Build a number of this storage size from a plang value — plang in, plang out. The value
    /// lowers itself through its own <c>Clr</c> door; a value that can't be this kind throws loud there
    /// (the courier converts that to <c>data.Fail</c>). No default, no catch, no null.</summary>
    public abstract global::app.type.item.number.@this Create(global::app.type.item.@this value);

    /// <summary>Write a number of this kind to the wire — the kind emits its own token.</summary>
    public abstract void Write(global::app.type.item.number.@this value, global::app.channel.serializer.IWriter writer);

    /// <summary>Read a number of this kind off the wire — the inverse of <see cref="Write"/>.</summary>
    public abstract global::app.type.item.@this Read<TReader>(ref TReader reader)
        where TReader : global::app.channel.serializer.IReader, allows ref struct;

    public override string ToString() => Name;
}
