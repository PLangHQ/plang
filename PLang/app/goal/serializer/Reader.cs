namespace app.goal.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>goal</c> — a <c>.pr</c>
/// payload materializing back into a <see cref="app.goal.@this"/>. The read-side mirror of
/// <see cref="app.goal.@this.Output"/>: the goal walks the handed <see cref="app.channel.serializer.IReader"/>
/// in place (the channel already made the one reader and positioned it at the first token), each step
/// via the sibling <see cref="app.goal.steps.step.serializer.Reader"/> and each sub-goal via this
/// reader's own recursion. The Goal backref + Synthetic are stamped by the caller (goal.list load).
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    private readonly global::app.goal.steps.step.serializer.Reader _step = new();

    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("goal", kind);

        string name = "";
        string? description = null, comment = null, hash = null, builderVersion = null;
        global::app.type.item.choice.@this<global::app.goal.Visibility> visibility = global::app.goal.Visibility.Private;
        global::app.type.item.path.@this? path = null;
        bool isSetup = false, isEvent = false, isSystem = false, isTest = false;
        var steps = new global::app.goal.steps.@this();
        var goals = new System.Collections.Generic.List<global::app.goal.@this>();

        reader.BeginObject();
        while (reader.NextName(out var field))
        {
            switch (field)
            {
                case "name": name = reader.String(); break;
                case "description": description = reader.String(); break;
                case "comment": comment = reader.String(); break;
                case "steps":
                    reader.BeginArray();
                    while (reader.NextElement())
                        steps.Add((global::app.goal.steps.step.@this)_step.Read(ref reader, kind, ctx));
                    reader.EndArray();
                    break;
                case "goals":
                    reader.BeginArray();
                    while (reader.NextElement())
                        goals.Add((global::app.goal.@this)Read(ref reader, kind, ctx));
                    reader.EndArray();
                    break;
                case "visibility":
                    visibility = global::app.type.item.choice.@this<global::app.goal.Visibility>.Parse(reader.String());
                    break;
                case "path": path = global::app.type.item.path.@this.Resolve(reader.String(), ctx.Context); break;
                // prPath is DERIVED from Path (its init is a no-op) — consume and discard.
                case "prPath": reader.Skip(); break;
                case "hash": hash = reader.String(); break;
                case "builderVersion": builderVersion = reader.String(); break;
                case "isSetup": isSetup = reader.Bool(); break;
                case "isEvent": isEvent = reader.Bool(); break;
                case "isSystem": isSystem = reader.Bool(); break;
                case "isTest": isTest = reader.Bool(); break;
                default: reader.Skip(); break;
            }
        }
        reader.EndObject();

        return new global::app.goal.@this
        {
            Name = name,
            Description = description,
            Comment = comment,
            Steps = steps,
            Goals = goals,
            Visibility = visibility,
            Path = path,
            Hash = hash,
            BuilderVersion = builderVersion,
            IsSetup = isSetup,
            IsEvent = isEvent,
            IsSystem = isSystem,
            IsTest = isTest,
        };
    }
}
