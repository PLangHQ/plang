namespace app.goal;

// The goal IS a plang value (item) — see step/actions/action/this.Item.cs for the ruling and the
// transition notes. The engine reads the typed internals (Name, Steps, Goals, …) directly; the item
// faces are the boundary only. Output delegates to the reflection (*) kind during the transition so
// the .pr wire stays byte-identical; the explicit Write + serializer/Reader.cs replace it, and the
// clr<goal> carrier at the consumers (build write, %goal% navigation) collapses to Data<goal>.
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>The goal's own type entity — an item names its own type. Distinct from GoalCall
    /// ("goal.call") and the goal channel; the reverse-name index already carried "goal" for this
    /// class, the item flip only adds the forward name→type slot.</summary>
    protected internal override global::app.type.@this Type => new("goal", typeof(@this));

    /// <summary>A structure, never a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>The goal writes ITSELF. TRANSITIONAL: routes through the reflection (*) kind — the
    /// bare [Store] tagged shape, identical to the pre-item clr-host write.</summary>
    public override async System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
        => await new global::app.type.item.kind.reflection.@this(context).Output(this, writer, mode, context);
}
