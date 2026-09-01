namespace app.goal.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>goal</c> — a <c>.pr</c>
/// payload materializing back into a <see cref="app.goal.@this"/>. The read-side mirror of
/// <see cref="app.goal.@this.Output"/>.
///
/// <para>The goal is the <b>binary→json content boundary</b>: a <c>.pr</c> arrives as raw content
/// (a scalar <c>value.Reader</c> over the file bytes — <c>binary/pr</c>), not a pre-tokenized wire
/// reader. The goal owns the fact that "a .pr is json", so it parses its own content into a json
/// reader ONCE here, then <see cref="Walk"/> walks it in place — each step via the sibling
/// <see cref="app.goal.step.serializer.Reader"/>, each sub-goal via <see cref="Walk"/>'s own
/// recursion (no per-level re-parse). The Goal backref + Synthetic are stamped by the caller
/// (goal.list load).</para>
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    private readonly global::app.goal.step.serializer.Reader _step = new();

    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("goal", kind);
        var raw = reader.RawValue();
        if (raw.Length == 0) return new global::app.type.item.@null.@this("goal", kind);
        var utf8 = new System.Text.Json.Utf8JsonReader(raw);
        utf8.Read();
        var json = new global::app.channel.serializer.json.Reader(utf8, raw);
        return Walk(ref json, ctx);
    }

    // Walks a goal object off the parsed json reader in place; sub-goals recurse through the SAME
    // reader (no re-parse). Steps ride the sibling step reader.
    // SHELL-FIRST: the goal is constructed empty so its steps and sub-goals can be born holding it,
    // then its own scalars are filled as they arrive. `parent` is null for the root goal in a .pr
    // file (it has none) and the enclosing goal for every sub-goal — a birth fact either way, so
    // nothing repairs Parent afterwards.
    private global::app.goal.@this Walk(ref global::app.channel.serializer.json.Reader reader,
        global::app.type.reader.ReadContext ctx, global::app.goal.@this? parent = null)
    {
        var goal = new global::app.goal.@this { Parent = parent };

        reader.BeginObject();
        while (reader.NextName(out var field))
        {
            switch (field)
            {
                case "name": goal.Name = reader.String(); break;
                case "description": goal.Description = reader.String(); break;
                case "comment": goal.Comment = reader.String(); break;
                case "step":
                    reader.BeginArray();
                    while (reader.NextElement())
                        goal.Step.Add(_step.Read(ref reader, ctx, goal));   // born knowing its goal
                    reader.EndArray();
                    break;
                case "child":
                    reader.BeginArray();
                    while (reader.NextElement())
                        goal.Child.Add(Walk(ref reader, ctx, goal));        // born knowing its parent
                    reader.EndArray();
                    break;
                case "visibility":
                    goal.Visibility = global::app.type.item.choice.@this<global::app.goal.Visibility>.Parse(reader.String());
                    break;
                case "path": goal.Path = global::app.type.item.path.@this.Resolve(reader.String(), ctx.Context); break;
                // prPath is DERIVED from Path — consume and discard.
                case "prPath": reader.Skip(); break;
                case "hash": goal.Hash = reader.String(); break;
                case "builderVersion": goal.BuilderVersion = reader.String(); break;
                case "isSetup": goal.IsSetup = reader.Bool(); break;
                case "isEvent": goal.IsEvent = reader.Bool(); break;
                case "isSystem": goal.IsSystem = reader.Bool(); break;
                case "isTest": goal.IsTest = reader.Bool(); break;
                default: reader.Skip(); break;
            }
        }
        reader.EndObject();
        return goal;
    }
}
