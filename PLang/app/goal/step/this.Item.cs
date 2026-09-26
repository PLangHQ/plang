namespace app.goal.step;

// The step IS a plang value (item) — see action/this.Item.cs for the ruling. The engine reads the
// typed internals (Index, Text, Actions, …) directly; the item faces are the boundary only. The step
// owns its wire: Output writes itself token by token, its serializer/Reader.cs reads itself back.
public partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>The step's own type entity — an item names its own type.</summary>
    protected internal override global::app.type.@this Type => new("step", typeof(@this));

    /// <summary>A step passes through; anything else is declined. A step is built by its goal, never
    /// converted from a value.</summary>
    public static @this? Create(object? raw, global::app.data.@this data)
    {
        if (raw is @this s) return s;
        data.Fail(new global::app.error.Error(
            $"%{data.Name}% holds a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name ?? "null"} — " +
            "a step is built by its goal, never converted from a value.", "CreateItemDeclined", 400));
        return null;
    }

    /// <summary>A structure, never a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>The step takes its own children. A write to <c>code</c> — the builder handing over
    /// what it just compiled — is the step CONSTRUCTING those actions, not a slot assignment: each one
    /// is born holding this step, the same birth fact a .pr load gives them. The rows ride the action's
    /// own reader, so the wire shape lives in one place. Every other key falls to the reflected
    /// default.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Set(
        string key, bool isIndex, object? value, global::app.actor.context.@this context)
    {
        if (isIndex || !string.Equals(key, "code", System.StringComparison.OrdinalIgnoreCase))
            return await base.Set(key, isIndex, value, context);

        var binding = value as global::app.data.@this;
        var incoming = binding != null ? await binding.Value() : value as global::app.type.item.@this;

        // The reader is born holding THIS step, so every action it makes is born holding it too —
        // the value bridges its own format, the reader carries the parent. Nothing is stamped.
        var reader = new global::app.goal.step.action.serializer.Reader(this);
        var node = new global::app.goal.step.action.list.@this();
        foreach (var row in Rows(incoming, context))
            if (row is global::app.goal.step.action.@this built) node.Add(built);
            else if (row.Read(reader, null, context) is global::app.goal.step.action.@this made) node.Add(made);
            else throw new System.NotSupportedException(
                $"cannot build an action from a {row.Type.Name} — an action reads from its own wire shape.");
        _code = node;
        return this;
    }

    /// <summary>The rows of an incoming action write. A several-valued write enumerates ITSELF — a
    /// plang list its elements, a json array its own (each value owns how it is walked); a single
    /// action is one row.</summary>
    private static System.Collections.Generic.IEnumerable<global::app.type.item.@this> Rows(
        global::app.type.item.@this? incoming, global::app.actor.context.@this context)
    {
        if (incoming == null) yield break;
        if (incoming is global::app.goal.step.action.@this) { yield return incoming; yield break; }
        var any = false;
        foreach (var (_, row) in incoming.EnumerateItems(context))
        {
            if (row.Peek() is { } item && !ReferenceEquals(item, incoming)) { any = true; yield return item; }
        }
        if (!any) yield return incoming;
    }

    /// <summary>The step writes ITSELF — its bare [Store] shape, singular keys, nulls omitted. Its
    /// <c>code</c> is the action list's own JSON tree (each action writes itself); <c>line</c> writes
    /// itself; <c>warning</c> is written when the build left any. The DEBUG view (the
    /// live --debug channel, never the persisted wire) routes through the reflection (*) kind.</summary>
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
        writer.Name("line"); Line.Output(writer);
        if (Comment != null) { writer.Name("comment"); writer.String(Comment); }
        writer.Name("code");
        await Code.Output(writer, mode, context);   // the action.list writes its own bare array
        if (Intent != null) { writer.Name("intent"); writer.String(Intent); }
        if (Source != null) { writer.Name("source"); writer.String(Source); }
        if (Warning.Count > 0)
        {
            writer.Name("warning");
            writer.BeginArray(Warning.Count);
            foreach (var warning in Warning)
            {
                writer.BeginObject();
                writer.Name("key"); writer.String(warning.Key);
                writer.Name("message"); writer.String(warning.Message);
                writer.EndObject();
            }
            writer.EndArray();
        }
        writer.Name("waitForExecution"); writer.Bool(WaitForExecution);
        writer.EndObject();
    }
}
