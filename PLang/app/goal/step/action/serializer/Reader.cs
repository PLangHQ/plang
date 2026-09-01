namespace app.goal.step.action.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>action</c> — the read-side
/// mirror of <see cref="app.goal.step.action.@this.Output"/>. Walks the handed
/// <see cref="app.channel.serializer.IReader"/> in place (the channel already made the one reader and
/// positioned it): the action's bare <c>[Store]</c> shape <c>{module, action, parameters[],
/// defaults?[], modifiers[]}</c>. Parameter/default rows ride the existing <c>@schema:data</c> reader.
/// A modifier rides action's own shape — each element in the <c>modifiers</c> array is populated as the
/// subtype so catalog/Is asks answer "modifier". Synthetic + the Goal backref are stamped by the caller
/// (goal.list load).
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    // Lazy — the action reads its Child steps through the step reader, which reads its actions back
    // through THIS reader. A `new()` field would recurse at construction; lazy breaks the cycle.
    private global::app.goal.step.serializer.Reader? _stepReader;
    private global::app.goal.step.serializer.Reader StepReader => _stepReader ??= new();

    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("action", kind);
        var action = new global::app.goal.step.action.@this();
        // Provenance at birth: an action READ from a .pr is authored, not injected — so it is
        // non-synthetic. Stamped here (the reader knows authored mode), never in a post-load loop.
        action.Synthetic = false;
        Populate(ref reader, action, ctx);
        return action;
    }

    /// <summary>The CONSTRUCTION door — the step reader calls this concretely, handing the step it
    /// has already built. The action is born knowing its step; nothing stamps it afterwards.
    /// The interface door above survives only until the graft and recovery stop routing actions
    /// through the type-reader registry, at which point this is the only way to make one.</summary>
    public global::app.goal.step.action.@this Read<TReader>(ref TReader reader,
        global::app.type.reader.ReadContext ctx, global::app.goal.step.@this step)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        var action = new global::app.goal.step.action.@this { Step = step, Synthetic = false };
        Populate(ref reader, action, ctx, step);
        return action;
    }

    // Fills a fresh action (or its modifier subtype) off the handed reader — the shared walk, so a
    // modifier element (same wire as an action) populates the subtype instance without re-parsing.
    // `step` is the action's own step when the construction door was used; null on the legacy
    // registry door. It flows to the children this action owns: its modifiers take the same step,
    // and its child steps take that step's goal — the chain self-feeds, nothing else is threaded.
    private void Populate<TReader>(ref TReader reader,
        global::app.goal.step.action.@this action, global::app.type.reader.ReadContext ctx,
        global::app.goal.step.@this? step = null)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        var dataReader = new global::app.data.reader.@this();
        reader.BeginObject();
        while (reader.NextName(out var name))
        {
            switch (name)
            {
                // The wire carries the module NAME; the action holds the element. Resolving here
                // means a .pr naming a module that no longer exists fails at LOAD (the registry
                // indexer throws) instead of mid-execution.
                case "module": action.Module = ctx.Context.App.Module[reader.String()]; break;
                // `name` is the canonical wire key (what Output writes and every .pr carries).
                // `action` is the LLM's clearer alias — the compile schema asks the model for
                // `action`, so the compile-response read accepts it here. One read door, both keys;
                // the persisted wire stays `name` (no .pr migration).
                case "name": case "action": action.Name = reader.String(); break;
                // `parameter` is canonical (Output writes it, the schema teaches it). `parameters` is
                // accepted too: the schema rides as a prompt hint, and the LLM naturally pluralizes an
                // array field name regardless — so the read tolerates the plural. Persisted wire stays
                // singular. Same for modifier/modifiers below.
                case "parameter": case "parameters":
                    reader.BeginArray();
                    while (reader.NextElement())
                        action.Parameter.Add(dataReader.Read(reader.RawValue(), ctx));
                    reader.EndArray();
                    break;
                case "default":
                    action.Default = new();
                    reader.BeginArray();
                    while (reader.NextElement())
                        action.Default.Add(dataReader.Read(reader.RawValue(), ctx));
                    reader.EndArray();
                    break;
                case "modifier": case "modifiers":
                    reader.BeginArray();
                    while (reader.NextElement())
                    {
                        // A modifier belongs to its action's step — handed over at construction,
                        // null only on the legacy registry door where no step is known.
                        var modifier = new global::app.goal.step.action.modifier.@this { Step = step };
                        Populate(ref reader, modifier, ctx, step);
                        action.Modifier.Add(modifier);
                    }
                    reader.EndArray();
                    break;
                case "child":
                    var childSteps = new global::app.goal.step.list.@this();   // Add each step into the node
                    reader.BeginArray();
                    while (reader.NextElement())
                        childSteps.Add(step != null
                            ? StepReader.Read(ref reader, ctx, step.Goal)   // chain self-feeds: the step's goal
                            : (global::app.goal.step.@this)StepReader.Read(ref reader, null, ctx));
                    reader.EndArray();
                    action.Child = childSteps;
                    break;
                default: reader.Skip(); break;
            }
        }
        reader.EndObject();
    }
}
