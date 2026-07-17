namespace app.goal.steps.step;

// The step IS a plang value (item) — see action/this.Item.cs for the ruling and transition notes.
// The engine reads the typed internals (Index, Text, Actions, …) directly; the item faces are the
// boundary only. Output delegates to the reflection (*) kind during the transition so the .pr wire
// stays byte-identical; the explicit Write + serializer/Reader.cs replace it in the follow-up.
public partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>The step's own type entity — an item names its own type.</summary>
    protected internal override global::app.type.@this Type => new("step", typeof(@this));

    /// <summary>A structure, never a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>The step writes ITSELF. TRANSITIONAL: routes through the reflection (*) kind — the
    /// bare [Store] tagged shape, identical to the pre-item clr-host write.</summary>
    public override async System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
        => await new global::app.type.item.kind.reflection.@this(context).Output(this, writer, mode, context);
}
