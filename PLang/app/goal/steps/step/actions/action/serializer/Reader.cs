namespace app.goal.steps.step.actions.action.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>action</c> — the read-side
/// mirror of <see cref="app.goal.steps.step.actions.action.@this.Output"/>. Walks the handed
/// <see cref="app.channel.serializer.IReader"/> in place (the channel already made the one reader and
/// positioned it): the action's bare <c>[Store]</c> shape <c>{module, action, parameters[],
/// defaults?[], modifiers[]}</c>. Parameter/default rows ride the existing <c>@schema:data</c> reader.
/// A modifier rides action's own shape — each element in the <c>modifiers</c> array is populated as the
/// subtype so catalog/Is asks answer "modifier". Synthetic + the Goal backref are stamped by the caller
/// (goal.list load).
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("action", kind);
        var action = new global::app.goal.steps.step.actions.action.@this();
        Populate(ref reader, action, ctx);
        return action;
    }

    // Fills a fresh action (or its modifier subtype) off the handed reader — the shared walk, so a
    // modifier element (same wire as an action) populates the subtype instance without re-parsing.
    private void Populate<TReader>(ref TReader reader,
        global::app.goal.steps.step.actions.action.@this action, global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        var dataReader = new global::app.data.reader.@this();
        reader.BeginObject();
        while (reader.NextName(out var name))
        {
            switch (name)
            {
                case "module": action.Module = reader.String(); break;
                case "action": action.ActionName = reader.String(); break;
                case "parameters":
                    reader.BeginArray();
                    while (reader.NextElement())
                        action.Parameters.Add(dataReader.Read(reader.RawValue(), ctx));
                    reader.EndArray();
                    break;
                case "defaults":
                    action.Defaults = new();
                    reader.BeginArray();
                    while (reader.NextElement())
                        action.Defaults.Add(dataReader.Read(reader.RawValue(), ctx));
                    reader.EndArray();
                    break;
                case "modifiers":
                    reader.BeginArray();
                    while (reader.NextElement())
                    {
                        var modifier = new global::app.goal.steps.step.actions.action.modifier.@this();
                        Populate(ref reader, modifier, ctx);
                        action.Modifiers.Add(modifier);
                    }
                    reader.EndArray();
                    break;
                default: reader.Skip(); break;
            }
        }
        reader.EndObject();
    }
}
