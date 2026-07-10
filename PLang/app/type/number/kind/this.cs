namespace app.type.number.kind;

/// <summary>
/// A number STORAGE kind — one of the 15 sizes a number can be held in (int, long, decimal, …).
/// The kind owns how to BUILD a value of its size from a raw value, how to build one from a double,
/// and how to READ/WRITE one on the wire. It rides the shared <see cref="app.type.kind.@this"/>
/// door (<c>App.Type.Kind[name|clrType]</c>, same selection as json/list/dict), declaring its
/// <see cref="ClrForm"/>; the value-navigation verbs on the base stay unsupported — a number is a
/// leaf, not navigated. The cross-kind RELATION (which size an overflow climbs to) is NOT here —
/// that's the Ladder's, in <c>number/this.Ladder.cs</c>.
///
/// <para>Most kinds build by <c>ChangeType</c> to their <see cref="ClrForm"/> (the base default);
/// the four CLR can't reach (BigInteger/Int128/UInt128/Half) override <see cref="Build"/>.</para>
/// </summary>
public abstract class @this : global::app.type.kind.@this
{
    protected @this(string name, global::app.actor.context.@this? context) : base(name, context) { }

    /// <summary>The CLR storage type of this kind (<c>typeof(int)</c>, …) — the door's clr→kind key.</summary>
    public abstract override System.Type? ClrForm { get; }

    /// <summary>Build a boxed CLR value of this kind's storage type from a raw value. Default:
    /// <c>ChangeType</c> to <see cref="ClrForm"/> (parses strings, converts numerics, rounds to
    /// integer targets) — the kinds ChangeType can't reach override this.</summary>
    public virtual object Build(object value)
        => System.Convert.ChangeType(value, ClrForm, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Build a number of this kind from a double — most keep the double; the low-precision
    /// binary floats (half/float) narrow to their storage first.</summary>
    public virtual global::app.type.number.@this FromDouble(double m) => global::app.type.number.@this.From(m);

    /// <summary>Write a number of this kind to the wire — the kind emits its own token (a native
    /// numeric primitive, or the lossless invariant string for the kinds beyond the writer's
    /// vocabulary: Int128/UInt128/BigInteger).</summary>
    public abstract void Write(global::app.type.number.@this value, global::app.channel.serializer.IWriter writer);

    /// <summary>Read a number of this kind off the wire — the inverse of <see cref="Write"/>,
    /// pulling the matching token and borning the number at this exact size.</summary>
    public abstract global::app.type.item.@this Read<TReader>(ref TReader reader)
        where TReader : global::app.channel.serializer.IReader, allows ref struct;
}
