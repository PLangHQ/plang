namespace app.goal.step;

// The step IS a plang value (item) — see action/this.Item.cs for the ruling. The engine reads the
// typed internals (Index, Text, Actions, …) directly; the item faces are the boundary only. The step
// owns its wire: Output writes itself token by token, its serializer/Reader.cs reads itself back.
public partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>The step's own type entity — an item names its own type.</summary>
    protected internal override global::app.type.@this Type => new("step", typeof(@this));

    /// <summary>The step builds ITSELF from a dict — the read-side twin of <see cref="Output"/>, the
    /// same <c>{index, text, lineNumber, comment?, action, …}</c> shape. Its actions read through their
    /// own door (a step owns its action chain). A non-dict, non-step raw is surfaced as a keyed error,
    /// never swallowed to null.</summary>
    public static @this? Create(object? raw, global::app.data.@this data)
    {
        if (raw is @this s) return s;
        if (raw is not global::app.type.item.dict.@this d)
        {
            data.Fail(new global::app.error.Error(
                $"cannot build a step from a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name ?? "null"} — " +
                $"a step reads from a dict of {{index, text, action}}.", "StepShape", 400));
            return null;
        }
        var step = new @this
        {
            Index = d.Get("index")?.Clr<int>() ?? 0,
            Text = d.Get("text")?.Peek()?.ToString() ?? "",
            LineNumber = d.Get("lineNumber")?.Clr<int>() ?? 0,
            Indent = d.Get("indent")?.Clr<int>() ?? 0,
            Comment = d.Get("comment")?.Peek()?.ToString(),
            Intent = d.Get("intent")?.Peek()?.ToString(),
        };
        step.Formal = d.Get("formal")?.Peek()?.ToString();
        step.Source = d.Get("source")?.Peek()?.ToString();
        if (d.Get("action")?.Peek() is global::app.type.item.list.@this acts)
        {
            var list = new System.Collections.Generic.List<global::app.goal.step.action.@this>();
            foreach (var row in acts.Items)
                if (Made<global::app.goal.step.action.@this>(row.Peek(), data) is { } a) list.Add(a);
            step.Action = new(list);
        }
        return step;
    }

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
        if (Comment != null) { writer.Name("comment"); writer.String(Comment); }
        writer.Name("action");
        writer.BeginArray(Action.CountRaw);
        foreach (var a in Action.Elements) await a.Output(writer, mode, context);
        writer.EndArray();
        if (Intent != null) { writer.Name("intent"); writer.String(Intent); }
        if (Formal != null) { writer.Name("formal"); writer.String(Formal); }
        if (Source != null) { writer.Name("source"); writer.String(Source); }
        writer.Name("waitForExecution"); writer.Bool(WaitForExecution);
        writer.EndObject();
    }
}
