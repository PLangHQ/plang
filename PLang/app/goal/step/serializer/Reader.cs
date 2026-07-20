namespace app.goal.step.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>step</c> — the read-side
/// mirror of <see cref="app.goal.step.@this.Output"/>. Walks the handed
/// <see cref="app.channel.serializer.IReader"/> in place: the step's bare <c>[Store]</c> shape, each
/// action via the sibling <see cref="app.goal.step.action.serializer.Reader"/>. Fields
/// land in locals first so the step's <c>init</c> props construct once. The Goal backref + Synthetic
/// are stamped by the caller (goal.list load).
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    private readonly global::app.goal.step.action.serializer.Reader _action = new();

    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("step", kind);

        int index = 0, lineNumber = 0, indent = 0;
        string text = "";
        string? comment = null, intent = null, formal = null, source = null;
        bool waitForExecution = true;
        var actions = new System.Collections.Generic.List<global::app.goal.step.action.@this>();

        reader.BeginObject();
        while (reader.NextName(out var name))
        {
            switch (name)
            {
                case "index": index = (int)reader.Long(); break;
                case "text": text = reader.String(); break;
                case "lineNumber": lineNumber = (int)reader.Long(); break;
                case "indent": indent = (int)reader.Long(); break;
                case "comment": comment = reader.String(); break;
                case "actions":
                    reader.BeginArray();
                    while (reader.NextElement())
                        actions.Add((global::app.goal.step.action.@this)_action.Read(ref reader, kind, ctx));
                    reader.EndArray();
                    break;
                case "intent": intent = reader.String(); break;
                case "formal": formal = reader.String(); break;
                case "source": source = reader.String(); break;
                case "waitForExecution": waitForExecution = reader.Bool(); break;
                default: reader.Skip(); break;
            }
        }
        reader.EndObject();

        return new global::app.goal.step.@this
        {
            Index = index,
            Text = text,
            LineNumber = lineNumber,
            Indent = indent,
            Comment = comment,
            Action = new global::app.goal.step.action.list.@this(actions),
            Intent = intent,
            Formal = formal,
            Source = source,
            WaitForExecution = waitForExecution,
        };
    }
}
