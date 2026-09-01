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

    /// <summary>The CONSTRUCTION door — the goal reader calls this concretely, handing the goal it
    /// has already built. SHELL-FIRST: the step is constructed empty so its actions can be born
    /// holding it, then its own scalars are filled as they arrive off the stream. That ordering is
    /// the whole reason the step's read-time scalars are `internal set` rather than `init`.</summary>
    public global::app.goal.step.@this Read<TReader>(ref TReader reader,
        global::app.type.reader.ReadContext ctx, global::app.goal.@this goal)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        var step = new global::app.goal.step.@this { Goal = goal };   // shell first — children can hold it

        reader.BeginObject();
        while (reader.NextName(out var name))
        {
            switch (name)
            {
                case "index": step.Index = (int)reader.Long(); break;
                case "text": step.Text = reader.String(); break;
                case "lineNumber": step.LineNumber = (int)reader.Long(); break;
                case "indent": step.Indent = (int)reader.Long(); break;
                case "comment": step.Comment = reader.String(); break;
                case "action": case "actions":
                    reader.BeginArray();
                    while (reader.NextElement())
                        step.Action.Add(_action.Read(ref reader, ctx, step));   // born knowing its step
                    reader.EndArray();
                    break;
                case "intent": step.Intent = reader.String(); break;
                case "formal": step.Formal = reader.String(); break;
                case "source": step.Source = reader.String(); break;
                case "waitForExecution": step.WaitForExecution = reader.Bool(); break;
                default: reader.Skip(); break;
            }
        }
        reader.EndObject();
        return step;
    }

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("step", kind);

        int index = 0, lineNumber = 0, indent = 0;
        string text = "";
        string? comment = null, intent = null, formal = null, source = null;
        bool waitForExecution = true;
        var actions = new global::app.goal.step.action.list.@this();   // Add each action straight into the node

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
                // `action` is canonical (Output writes it); `actions` is the LLM's natural plural for
                // the list (same tolerance as parameter/parameters on the action reader).
                case "action": case "actions":
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
            Action = actions,
            Intent = intent,
            Formal = formal,
            Source = source,
            WaitForExecution = waitForExecution,
        };
    }
}
