namespace app.goal.steps.step;

// The step IS a plang value (item) — see action/this.Item.cs for the ruling. The engine reads the
// typed internals (Index, Text, Actions, …) directly; the item faces are the boundary only. The step
// owns its wire: Output writes itself token by token, its serializer/Reader.cs reads itself back.
public partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>The step's own type entity — an item names its own type.</summary>
    protected internal override global::app.type.@this Type => new("step", typeof(@this));

    /// <summary>A structure, never a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>The step writes ITSELF — its bare [Store] shape in declaration order, singular keys,
    /// nulls omitted (byte-identical to the reflected write it replaces). Actions are action-shaped
    /// items (each writes itself). The DEBUG view (the live --debug channel, never the persisted wire)
    /// routes through the reflection (*) kind so diagnostic props (Errors/Warnings) ride.</summary>
    public override async System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        if (mode == global::app.View.Debug)
        {
            await new global::app.type.item.kind.reflection.@this(context).Output(this, writer, mode, context);
            return;
        }
        writer.BeginObject();
        writer.Name("index"); writer.Int(Index);
        writer.Name("text"); writer.String(Text);
        writer.Name("lineNumber"); writer.Int(LineNumber);
        writer.Name("indent"); writer.Int(Indent);
        if (Comment != null) { writer.Name("comment"); writer.String(Comment); }
        writer.Name("actions");
        writer.BeginArray(Actions.Count);
        foreach (var a in Actions) await a.Output(writer, mode, context);
        writer.EndArray();
        if (Intent != null) { writer.Name("intent"); writer.String(Intent); }
        if (Formal != null) { writer.Name("formal"); writer.String(Formal); }
        if (Source != null) { writer.Name("source"); writer.String(Source); }
        writer.Name("waitForExecution"); writer.Bool(WaitForExecution);
        writer.EndObject();
    }
}
