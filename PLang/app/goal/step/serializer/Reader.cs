namespace app.goal.step.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>step</c> — the read-side
/// mirror of <see cref="app.goal.step.@this.Output"/>. Walks the handed
/// <see cref="app.channel.serializer.IReader"/> in place: the step's bare <c>[Store]</c> shape, each
/// action via the sibling <see cref="app.goal.step.action.serializer.Reader"/>.
/// <para>The reader is BORN with the goal whose steps it reads, so every step it makes is born
/// holding that goal. Having no parameterless constructor is what keeps it out of the type-reader
/// registry: the registry mints only readers that need no parent, so there is no parentless way to
/// make a step.</para>
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    private readonly global::app.goal.@this _goal;

    public Reader(global::app.goal.@this goal) => _goal = goal;

    public string Kind => global::app.type.reader.@this.AnyKind;

    /// <summary>The one door. SHELL-FIRST: the step is constructed empty so its actions can be born
    /// holding it, then its own scalars are filled as they arrive off the stream. That ordering is
    /// the whole reason the step's read-time scalars are `internal set` rather than `init`.
    /// A null element is consumed and answered as the null citizen; the caller drops it.</summary>
    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("step", kind);

        var step = new global::app.goal.step.@this { Goal = _goal };   // shell first — children hold it
        var action = new global::app.goal.step.action.serializer.Reader(step);

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
                case "action":
                    reader.BeginArray();
                    while (reader.NextElement())
                        if (action.Read(ref reader, null, ctx) is global::app.goal.step.action.@this a)
                            step.Action.Add(a);
                    reader.EndArray();
                    break;
                case "intent": step.Intent = reader.String(); break;
                case "source": step.Source = reader.String(); break;
                case "waitForExecution": step.WaitForExecution = reader.Bool(); break;
                // Every key the step writes is read above; an unknown one means another builder wrote it.
                default: throw new global::app.error.PrFormatOutdatedException($"step key '{name}' isn't in this .pr format");
            }
        }
        reader.EndObject();
        return step;
    }
}
