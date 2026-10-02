namespace app.goal.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>goal</c> — a <c>.pr</c>
/// payload materializing back into a <see cref="app.goal.@this"/>. The read-side mirror of
/// <see cref="app.goal.@this.Output"/>.
///
/// <para>It reads real tokens only: an object is a goal written whole (a <c>.pr</c> — goal's own
/// decode hands it the file through a json reader), a string is a goal written by its name (a call's
/// row), read as that name and selected when its slot reads it. <see cref="Walk"/> walks the object in
/// place — each step via the sibling <see cref="app.goal.step.serializer.Reader"/>, each sub-goal via its
/// own recursion.</para>
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    // The goal reader stays registered: a .pr FILE has no parent, so this one needs none. Its
    // children's readers are born per goal, each holding the goal it reads for.
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("goal", kind);
        // a goal written by its name is that name
        if (reader.Peek() == global::app.type.format.TokenKind.String)
            return new global::app.type.item.text.@this(reader.String());
        return Walk(ref reader, ctx);
    }

    // Walks a goal object off the parsed json reader in place; sub-goals recurse through the SAME
    // reader (no re-parse). Steps ride the sibling step reader.
    // SHELL-FIRST: the goal is constructed empty so its steps and sub-goals can be born holding it,
    // then its own scalars are filled as they arrive. `parent` is null for the root goal in a .pr
    // file (it has none) and the enclosing goal for every sub-goal — a birth fact either way, so
    // nothing repairs Parent afterwards.
    private global::app.goal.@this Walk<TReader>(ref TReader reader,
        global::app.type.reader.ReadContext ctx, global::app.goal.@this? parent = null)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        var goal = new global::app.goal.@this { Parent = parent };
        var step = new global::app.goal.step.serializer.Reader(goal);   // born holding this goal

        var named = false;
        reader.BeginObject();
        while (reader.NextName(out var field))
        {
            switch (field)
            {
                case "name": goal.Name = reader.String(); named = true; break;
                case "comment": goal.Comment = reader.String(); break;
                case "step":
                    reader.BeginArray();
                    while (reader.NextElement())
                        if (step.Read(ref reader, null, ctx) is global::app.goal.step.@this s)
                            goal.Step.Add(s);
                    reader.EndArray();
                    break;
                case "child":
                    reader.BeginArray();
                    while (reader.NextElement())
                        goal.Child.Add(Walk(ref reader, ctx, goal));        // born knowing its parent
                    reader.EndArray();
                    break;
                // visibility derives from the goal's parent (a file's first goal is public) — the key
                // is still written, read by no one.
                case "visibility": reader.Skip(); break;
                case "path": goal.Path = global::app.type.item.path.@this.Resolve(reader.String(), ctx.Context); break;
                // prPath is DERIVED from Path — consume and discard.
                case "prPath": reader.Skip(); break;
                case "hash": goal.Hash = reader.String(); break;
                case "builderVersion": goal.BuilderVersion = reader.String(); break;
                // what a goal is (setup, system, a test) it answers from its path, and isEvent was never set: an
                // older .pr's keys are skipped by name
                case "isSetup" or "isEvent" or "isSystem" or "isTest": reader.Skip(); break;
                case "tag":
                    // Each tag reads itself through its own reader.
                    var tag = new global::app.type.item.tag.serializer.Reader();
                    reader.BeginArray();
                    while (reader.NextElement())
                        goal.Tag.Add(tag.Read(ref reader, null, ctx));
                    reader.EndArray();
                    break;
                // Every key the goal writes is read above. A key it doesn't know means another builder
                // wrote this .pr — skipping it would load a goal missing what that key held, silently.
                default: throw new global::app.error.PrFormatOutdatedException($"key '{field}' isn't in this .pr format", ctx.Origin);
            }
        }
        reader.EndObject();
        if (!named) throw new global::app.error.PrFormatOutdatedException("it has no 'name'", ctx.Origin);
        return goal;
    }
}
