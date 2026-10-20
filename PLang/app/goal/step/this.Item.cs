namespace app.goal.step;

// The step IS a plang value (item) — see action/this.Item.cs for the ruling. The engine reads the
// typed internals (Index, Text, Actions, …) directly; the item faces are the boundary only. The step
// owns its wire: Output writes itself token by token, its serializer/Reader.cs reads itself back.
[global::app.Attributes.PlangType("step")]
public partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    // what a program adds to the step — kept as long as the step is loaded
    private readonly global::app.type.item.kept.list.@this _kept = new();
    internal override global::app.type.item.kept.list.@this Kept => _kept;

    /// <summary>A step passes through; anything else is declined. A step is built by its goal, never
    /// converted from a value.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (raw is @this s) return s;
        data.Fail(new global::app.error.@this(
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
        var node = new global::app.goal.step.action.list.@this(_code.Type);
        foreach (var row in Rows(incoming, context))
            if (row is global::app.goal.step.action.@this built) node.Add(built);
            // a value that bridges its own format (a clr(json) element) reads through the action reader directly
            else if (row.Read(reader, null, context) is global::app.goal.step.action.@this made) node.Add(made);
            // a parsed plang container (a {item, json} the builder navigated to a dict/list) writes itself as
            // json and the one action reader reads those tokens — the action's wire shape stays in one reader
            else if ((row is global::app.type.item.dict.@this or global::app.type.item.list.@this)
                     && await FromJson(row, reader, context) is global::app.goal.step.action.@this fromPlang) node.Add(fromPlang);
            else throw new global::app.error.AppException(
                $"cannot build an action from a {row.Type.Name} — an action reads from its own wire shape.", "NotAnAction", 400);
        _code = node;
        return this;
    }

    // A parsed plang container writes itself as json, and the action reader (born with this step) reads those
    // tokens — the same wire shape a .pr holds. (Branch A will give the reader a structural reader over a plang
    // value, so this round-trip goes; today the value's only container serializer is its async json Output.)
    private async System.Threading.Tasks.ValueTask<global::app.goal.step.action.@this?> FromJson(
        global::app.type.item.@this row, global::app.goal.step.action.serializer.Reader reader,
        global::app.actor.context.@this context)
    {
        using var stream = new System.IO.MemoryStream();
        await using (var utf8 = new System.Text.Json.Utf8JsonWriter(stream))
            await row.Output(new global::app.type.item.kind.json.Writer(utf8, context.App.type.list, global::app.View.Out), global::app.View.Out, context);
        var bytes = stream.ToArray();
        var utf8Reader = new System.Text.Json.Utf8JsonReader(bytes);
        utf8Reader.Read();
        var tokens = new global::app.type.item.kind.json.Reader(utf8Reader, bytes);
        return reader.Read(ref tokens, null, new global::app.type.reader.ReadContext(context)) as global::app.goal.step.action.@this;
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
        global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        if (mode == global::app.View.Debug)
        {
            await base.Output(writer, mode, context);
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
        // a step the build could not fix: its error writes itself, as it does everywhere — the goal it belongs to does
        // not start
        if (Error != null) { writer.Name("error"); Error.Write(writer); }
        writer.EndObject();
    }
}
