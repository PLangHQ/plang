namespace app.type.item.setting.serializer;

/// <summary>
/// Reads a setting back symmetrically with how it was written. A new row is written as its own type
/// (<c>app.module.llm.setting</c>) and read back by that type: the reader registered for that settings
/// type takes the class from the type itself (its C# class), <see cref="_type"/> naming it. An old
/// <c>{setting, kind: &lt;path&gt;}</c> row reaches the reader registered under the base <c>setting</c>,
/// whose <see cref="_type"/> is null, and takes its class from the kind as the path. A fresh one of that
/// class takes each option the wire holds, each read through its own type's reader. An option the class no
/// longer has is skipped; one the row lacks keeps its default; a row whose class is gone reads as nothing.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    // The settings type this reader reads as — null for the one registered under the base `setting`, which
    // an old {setting, kind: <path>} row reaches by its kind.
    private readonly string? _type;
    public Reader() { }
    internal Reader(string type) { _type = type; }

    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("setting", kind);
        var types = ctx.Context.App.type.list;
        // the class comes from the type the row says it is (a new row), else — an old row — from its kind
        // as the path; a row whose class is gone reads as nothing, the other rows still load
        var setting = _type != null
            ? types.Contains(_type) && types[_type].ClrType is { IsAbstract: false } clr
                ? (global::app.type.item.setting.@this)System.Activator.CreateInstance(clr)!
                : null
            : kind != null ? ctx.Context.Setting.Fresh(kind) : null;
        if (setting is null)
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
