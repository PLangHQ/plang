namespace app.type.item.variable.serializer;

/// <summary>
/// A <c>.pr</c> row's <c>"variable"</c> list: each variable the row's value holds, written once at
/// build as its text and its code, so loading never parses —
/// <c>{"text": "%user.address[idx]%", "code": [{"variable": "user"}, {"property": "address"},
/// {"index": {"variable": [{"text": "%idx%", "code": [{"variable": "idx"}]}]}}]}</c>. Each hop writes
/// itself under its kind (<see cref="code.Hop.Output"/>); this reads them back.
/// </summary>
public sealed class Entry
{
    public void Write(global::app.type.format.IWriter writer, IReadOnlyList<variable.@this> variables)
    {
        writer.BeginArray(variables.Count);
        foreach (var v in variables)
        {
            writer.BeginObject();
            writer.Name("text");
            writer.String(v.Text);
            writer.Name("code");
            v.Code.Write(writer);
            writer.EndObject();
        }
        writer.EndArray();
    }

    public IReadOnlyList<variable.@this> Read(ref global::app.type.item.kind.json.Reader reader,
        global::app.type.reader.ReadContext ctx)
    {
        var read = new List<variable.@this>();
        reader.BeginArray();
        while (reader.NextElement())
        {
            string? text = null;
            List<code.Hop>? hops = null;
            reader.BeginObject();
            while (reader.NextName(out var key))
            {
                switch (key)
                {
                    case "text": text = reader.String(); break;
                    case "code": hops = Code(ref reader, ctx); break;
                    default: throw new global::app.error.PrFormatOutdatedException($"variable key '{key}' isn't in this .pr format", ctx.Origin);
                }
            }
            reader.EndObject();
            if (text == null || hops is not { Count: > 0 } || hops[0] is not code.Variable)
                throw new global::app.error.PrFormatOutdatedException("a variable in the .pr is its text and its code, the root first", ctx.Origin);
            read.Add(new variable.@this(text, new code.@this(hops)));
        }
        reader.EndArray();
        return read;
    }

    private List<code.Hop> Code(ref global::app.type.item.kind.json.Reader reader, global::app.type.reader.ReadContext ctx)
    {
        var hops = new List<code.Hop>();
        reader.BeginArray();
        while (reader.NextElement())
        {
            reader.BeginObject();
            if (!reader.NextName(out var kind))
                throw new global::app.error.PrFormatOutdatedException("a hop in the .pr names its kind", ctx.Origin);
            string? name = null;
            code.Hop hop;
            switch (kind)
            {
                case "variable":
                    hop = new code.Variable(reader.String());
                    break;
                case "property":
                    name = reader.String();
                    hop = new code.Property(name.StartsWith('!') ? name : "." + name, name);
                    break;
                case "index":
                    var key = Key(ref reader, ctx);
                    hop = new code.Index($"[{(key is variable.@this v ? v.Text : key.ToString())}]", key);
                    break;
                case "method":
                    name = reader.String();
                    var values = new List<global::app.type.item.@this>();
                    if (!reader.NextName(out var parameter) || parameter != "parameter")
                        throw new global::app.error.PrFormatOutdatedException("a method in the .pr lists its parameter", ctx.Origin);
                    reader.BeginArray();
                    var rows = new global::app.data.reader.@this();
                    while (reader.NextElement()) values.Add(rows.Read(ref reader, ctx).Peek());
                    reader.EndArray();
                    hop = new code.Method($".{name}(…)", name, new global::app.type.item.list.@this(values));
                    break;
                default:
                    throw new global::app.error.PrFormatOutdatedException($"hop kind '{kind}' isn't in this .pr format", ctx.Origin);
            }
            if (reader.NextName(out var extra))
                throw new global::app.error.PrFormatOutdatedException($"hop key '{extra}' isn't in this .pr format", ctx.Origin);
            reader.EndObject();
            hops.Add(hop);
        }
        reader.EndArray();
        return hops;
    }

    // An index key: {"number": 0}, {"text": "k"}, or {"variable": [<the key variable>]}.
    private global::app.type.item.@this Key(ref global::app.type.item.kind.json.Reader reader, global::app.type.reader.ReadContext ctx)
    {
        reader.BeginObject();
        if (!reader.NextName(out var kind))
            throw new global::app.error.PrFormatOutdatedException("an index in the .pr names its key", ctx.Origin);
        global::app.type.item.@this key = kind switch
        {
            "number" => global::app.type.item.number.@this.Create(reader.Number())!,
            "text" => new global::app.type.item.text.@this(reader.String()),
            "variable" => Read(ref reader, ctx).Single(),
            _ => throw new global::app.error.PrFormatOutdatedException($"index key '{kind}' isn't in this .pr format", ctx.Origin),
        };
        if (reader.NextName(out var extra))
            throw new global::app.error.PrFormatOutdatedException($"index key '{extra}' isn't in this .pr format", ctx.Origin);
        reader.EndObject();
        return key;
    }
}
