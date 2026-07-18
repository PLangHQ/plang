namespace app.goal.steps.step.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>step</c> — the read-side
/// mirror of <see cref="app.goal.steps.step.@this.Output"/>. Reads the step's bare <c>[Store]</c>
/// shape token by token and constructs the step natively; each action rides the
/// <see cref="app.goal.steps.step.actions.action.serializer.Reader"/>. Fields land in locals first
/// so the step's <c>init</c> props construct once (no reflection SetValue). The Goal backref +
/// Synthetic are stamped by the caller (goal.list load), not here.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
        => ReadStep(ref reader, ctx);

    // Static so the goal reader shares the one walk per step without a fresh registry lookup.
    internal static global::app.goal.steps.step.@this ReadStep<TReader>(
        ref TReader reader, global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        int index = 0, lineNumber = 0, indent = 0;
        string text = "";
        string? comment = null, intent = null, formal = null, source = null;
        bool waitForExecution = true;
        var actions = new global::app.goal.steps.step.actions.@this();

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
                        actions.Add(global::app.goal.steps.step.actions.action.serializer.Reader
                            .ReadAction(ref reader, isModifier: false, ctx));
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

        return new global::app.goal.steps.step.@this
        {
            Index = index,
            Text = text,
            LineNumber = lineNumber,
            Indent = indent,
            Comment = comment,
            Actions = actions,
            Intent = intent,
            Formal = formal,
            Source = source,
            WaitForExecution = waitForExecution,
        };
    }
}
