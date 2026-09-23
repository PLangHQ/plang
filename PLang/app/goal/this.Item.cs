namespace app.goal;

// The goal IS a plang value (item) — see step/actions/action/this.Item.cs for the ruling. The engine
// reads the typed internals (Name, Steps, Child, …) directly; the item faces are the boundary only.
// The goal owns its wire: Output writes itself token by token (each field a plang type that writes
// itself — path, choice, the step/goal children), its serializer/Reader.cs reads itself back.
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>The goal's own type entity — an item names its own type. Distinct from the goal
    /// channel; the reverse-name index already carried "goal" for this class, the item flip only adds
    /// the forward name→type slot.</summary>
    protected internal override global::app.type.@this Type => new("goal", typeof(@this));

    /// <summary>A goal passes through; anything else is declined. Program structure has one way in —
    /// its reader (<c>serializer/Reader.cs</c>) — and is never converted from a value.</summary>
    public static @this? Create(object? raw, global::app.data.@this data)
    {
        if (raw is @this g) return g;
        data.Fail(new global::app.error.Error(
            $"%{data.Name}% holds a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name ?? "null"} — " +
            "a goal is read from its .pr, never converted from a value.", "CreateItemDeclined", 400));
        return null;
    }

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
        writer.Name("step");
        await Step.Output(writer, mode, context);   // the step.list writes its own bare array
        writer.Name("child");
        writer.BeginArray(Child.Count);
        foreach (var g in Child) await g.Output(writer, mode, context);
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
        if (Tag.CountRaw > 0) { writer.Name("tag"); await Tag.Output(writer, mode, context); }
        writer.EndObject();
    }
}
