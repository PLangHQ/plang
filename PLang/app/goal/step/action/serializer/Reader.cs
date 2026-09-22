namespace app.goal.step.action.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>action</c> — the read-side
/// mirror of <see cref="app.goal.step.action.@this.Output"/>. Walks the handed
/// <see cref="app.channel.serializer.IReader"/> in place (the channel already made the one reader and
/// positioned it): the action's bare <c>[Store]</c> shape <c>{module, action, parameters[],
/// defaults?[], modifiers[]}</c>. Parameter/default rows ride the existing <c>@schema:data</c> reader.
/// A modifier rides action's own shape — each element in the <c>modifiers</c> array is populated as the
/// subtype so catalog/Is asks answer "modifier".
/// <para>The reader is BORN with the step whose actions it reads, so every action it makes is born
/// holding that step — the same birth fact one level up. Having no parameterless constructor is what
/// keeps it out of the type-reader registry: the registry mints only readers that need no parent
/// (values and file roots), so there is no parentless way to make an action.</para>
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    private readonly global::app.goal.step.@this _step;

    public Reader(global::app.goal.step.@this step) => _step = step;

    public string Kind => global::app.type.reader.@this.AnyKind;

    /// <summary>The one door. A null element is consumed and answered as the null citizen; the
    /// caller drops it.</summary>
    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("action", kind);
        // Provenance at birth: an action READ is authored, not injected — so it is non-synthetic.
        var action = new global::app.goal.step.action.@this { Step = _step, Synthetic = false };
        Populate(ref reader, action, ctx);
        return action;
    }

    // Fills a fresh action (or its modifier subtype) off the handed reader — the shared walk, so a
    // modifier element (same wire as an action) populates the subtype instance without re-parsing.
    // The children this action owns take their parent from the reader: modifiers and recovery
    // actions the same step, child steps that step's goal — the chain self-feeds.
    private void Populate<TReader>(ref TReader reader,
        global::app.goal.step.action.@this action, global::app.type.reader.ReadContext ctx)
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
                        // A modifier belongs to its action's step, like the action itself.
                        var modifier = new global::app.goal.step.action.modifier.@this { Step = _step };
                        Populate(ref reader, modifier, ctx);
                        action.Modifier.Add(modifier);
                    }
                    reader.EndArray();
                    break;
                case "recovery":
                    // Recovery actions are actions, read here like any other and born holding the
                    // SAME step as the action they recover — a real step, not an invented one.
                    reader.BeginArray();
                    while (reader.NextElement())
                        if (Read(ref reader, null, ctx) is global::app.goal.step.action.@this recovered)
                            action.Recovery.Add(recovered);
                    reader.EndArray();
                    break;
                case "child":
                    // chain self-feeds: a child step's goal is this action's step's goal
                    var childSteps = new global::app.goal.step.list.@this();
                    var stepReader = new global::app.goal.step.serializer.Reader(_step.Goal);
                    reader.BeginArray();
                    while (reader.NextElement())
                        if (stepReader.Read(ref reader, null, ctx) is global::app.goal.step.@this child)
                            childSteps.Add(child);
                    reader.EndArray();
                    action.Child = childSteps;
                    break;
                default: reader.Skip(); break;
            }
        }
        reader.EndObject();
    }
}
