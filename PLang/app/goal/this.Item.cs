namespace app.goal;

// The goal IS a plang value (item) — see step/actions/action/this.Item.cs for the ruling. The engine
// reads the typed internals (Name, Steps, Goals, …) directly; the item faces are the boundary only.
// The goal owns its wire: Output writes itself token by token (each field a plang type that writes
// itself — path, choice, the step/goal children), its serializer/Reader.cs reads itself back.
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>The goal's own type entity — an item names its own type. Distinct from GoalCall
    /// ("goal.call") and the goal channel; the reverse-name index already carried "goal" for this
    /// class, the item flip only adds the forward name→type slot.</summary>
    protected internal override global::app.type.@this Type => new("goal", typeof(@this));

    /// <summary>A structure, never a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>The goal writes ITSELF — its bare [Store] shape in declaration order, singular keys,
    /// nulls omitted. Each rich field writes itself: path (its relative string), visibility (the choice
    /// symbol), the step/sub-goal children (each an item). The DEBUG view routes through the reflection
    /// (*) kind so diagnostic props (Errors/Warnings) ride.</summary>
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
        writer.Name("name"); writer.String(Name);
        if (Description != null) { writer.Name("description"); writer.String(Description); }
        if (Comment != null) { writer.Name("comment"); writer.String(Comment); }
        writer.Name("steps");
        writer.BeginArray(Step.Count);
        foreach (var s in Step.list) await s.Output(writer, mode, context);
        writer.EndArray();
        writer.Name("goals");
        writer.BeginArray(Goals.Count);
        foreach (var g in Goals) await g.Output(writer, mode, context);
        writer.EndArray();
        writer.Name("visibility"); await Visibility.Output(writer, mode, context);
        if (Path != null) { writer.Name("path"); await Path.Output(writer, mode, context); }
        if (PrPath != null) { writer.Name("prPath"); await PrPath.Output(writer, mode, context); }
        if (Hash != null) { writer.Name("hash"); writer.String(Hash); }
        if (BuilderVersion != null) { writer.Name("builderVersion"); writer.String(BuilderVersion); }
        writer.Name("isSetup"); writer.Bool(IsSetup);
        writer.Name("isEvent"); writer.Bool(IsEvent);
        writer.Name("isSystem"); writer.Bool(IsSystem);
        writer.Name("isTest"); writer.Bool(IsTest);
        writer.EndObject();
    }
}
