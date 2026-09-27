namespace app.type.item.setting.serializer;

/// <summary>
/// Reads a setting back — <c>{setting, kind: goal.list.setting}</c>: the kind names the class, a new one
/// of it takes each option the wire holds, each read through its own type's reader. An option the class
/// no longer has is skipped (its row is older than the class); one the row lacks keeps its default; a
/// row whose class is gone reads as nothing.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("setting", kind);
        var types = ctx.Context.App.type.list;
        // a row whose class is gone reads as nothing — the other rows still load
        if (kind == null || types["setting"].kind[kind] is not global::app.type.item.setting.kind.@this known)
        {
            reader.Skip();
            return new global::app.type.item.@null.@this("setting", kind);
        }
        var setting = known.Create();
        var read = new Dictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase);
        reader.BeginObject();
        while (reader.NextName(out var name))
        {
            if (setting.Option(name) is { } option)
            {
                var type = types[option.PropertyType];
                var kindName = type.kind.IsEmpty ? null : type.kind.Name;
                read[name] = types.Reader.Reader(type.Name, kindName, ctx.Context).Read(ref reader, kindName, ctx);
            }
            else
                reader.Skip();
        }
        reader.EndObject();
        // each value into its option through the option's own type — the walk the CLI flags take
        var applied = setting.Apply(read, ctx.Context);
        return applied.Success ? setting : new global::app.type.item.@null.@this("setting", kind);
    }
}
