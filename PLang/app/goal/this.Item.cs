namespace app.goal;

// The goal IS a plang value (item) — see step/actions/action/this.Item.cs for the ruling. The engine
// reads the typed internals (Name, Steps, Child, …) directly; the item faces are the boundary only.
// The goal owns its wire: Output writes itself token by token (each field a plang type that writes
// itself — path, choice, the step/goal children), its serializer/Reader.cs reads itself back.
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>The goal's own type entity — an item names its own type. Distinct from GoalCall
    /// ("goal.call") and the goal channel; the reverse-name index already carried "goal" for this
    /// class, the item flip only adds the forward name→type slot.</summary>
    protected internal override global::app.type.@this Type => new("goal", typeof(@this));

    /// <summary>The goal builds ITSELF from a dict — the read-side twin of <see cref="Output"/>, the
    /// same <c>{name, description?, comment?, step, child, visibility, path?, …}</c> shape. Its steps
    /// and sub-goals read through their own door. A non-dict, non-goal raw is surfaced as a keyed
    /// error, never swallowed to null.</summary>
    public static @this? Create(object? raw, global::app.data.@this data)
    {
        if (raw is @this g) return g;
        if (raw is not global::app.type.item.dict.@this d)
        {
            data.Fail(new global::app.error.Error(
                $"cannot build a goal from a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name ?? "null"} — " +
                $"a goal reads from a dict of {{name, step, child}}.", "GoalShape", 400));
            return null;
        }
        var goal = new @this
        {
            Name = d.Get("name")?.Peek()?.ToString() ?? "",
            Comment = d.Get("comment")?.Peek()?.ToString(),
            IsSetup = d.Get("isSetup")?.Clr<bool>() ?? false,
            IsEvent = d.Get("isEvent")?.Clr<bool>() ?? false,
            IsSystem = d.Get("isSystem")?.Clr<bool>() ?? false,
            Visibility = d.Get("visibility")?.Peek()?.ToString() is { } vis
                ? global::app.type.item.choice.@this<global::app.goal.Visibility>.Parse(vis)
                : global::app.goal.Visibility.Private,
        };
        goal.Description = d.Get("description")?.Peek()?.ToString();
        goal.BuilderVersion = d.Get("builderVersion")?.Peek()?.ToString();
        goal.IsTest = d.Get("isTest")?.Clr<bool>() ?? false;
        if (d.Get("path")?.Peek()?.ToString() is { } p && data.Context != null)
            goal.Path = global::app.type.item.path.@this.Resolve(p, data.Context);
        if (d.Get("step")?.Peek() is global::app.type.item.list.@this steps)
        {
            var node = new global::app.goal.step.list.@this();
            foreach (var row in steps.Items)
                if (Made<global::app.goal.step.@this>(row.Peek(), data) is { } s) node.Add(s);
            goal.Step = node;
        }
        if (d.Get("child")?.Peek() is global::app.type.item.list.@this subs)
        {
            var list = new System.Collections.Generic.List<@this>();
            foreach (var row in subs.Items)
                if (Made<@this>(row.Peek(), data) is { } sg) list.Add(sg);
            goal.Child = list;
        }
        return goal;
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
        writer.EndObject();
    }
}
