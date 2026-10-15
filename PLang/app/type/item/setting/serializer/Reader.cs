namespace app.type.item.setting.serializer;

/// <summary>
/// Reads a setting back — the <paramref name="kind"/> handed in is the row's KEY path (its class's
/// address), through which actor/setting's index names the class; a fresh one of it takes each option the
/// wire holds, each read through its own type's reader. The reader never reads the class from a stored
/// kind, so an old <c>{setting, kind: …}</c> row and a new one read the same way. An option the class no
/// longer has is skipped; one the row lacks keeps its default; a row whose class is gone reads as nothing.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("setting", kind);
        var types = ctx.Context.App.type.list;
        // a row whose class is gone reads as nothing — the other rows still load
        if (kind == null || ctx.Context.Setting.Fresh(kind) is not { } setting)
        {
            reader.Skip();
            return new global::app.type.item.@null.@this("setting", kind);
        }
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
