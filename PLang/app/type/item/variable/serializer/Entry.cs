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
    public async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer, IReadOnlyList<variable.@this> variables)
    {
        writer.BeginArray(variables.Count);
        foreach (var v in variables)
        {
            writer.BeginObject();
            writer.Name("text");
            writer.String(v.Text);
            writer.Name("code");
            await v.Code.Output(writer);
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
            code.Hop? root = null;
            reader.BeginObject();
            while (reader.NextName(out var key))
            {
                switch (key)
                {
                    case "text": text = reader.String(); break;
                    case "code": root = Code(ref reader, ctx); break;
                    default: throw new global::app.error.PrFormatOutdatedException($"variable key '{key}' isn't in this .pr format", ctx.Origin);
                }
            }
            reader.EndObject();
            if (text == null || root is not code.Variable)
                throw new global::app.error.PrFormatOutdatedException("a variable in the .pr is its text and its code, the root first", ctx.Origin);
            var types = ctx.Context!.App.type.list;
            read.Add(new variable.@this(text, new code.@this(root, types[typeof(code.@this)]), types[typeof(variable.@this)]));
        }
        reader.EndArray();
        return read;
    }

    // The code's hops, the first of them holding the rest; null when the array is empty.
    private code.Hop? Code(ref global::app.type.item.kind.json.Reader reader, global::app.type.reader.ReadContext ctx)
    {
        reader.BeginArray();
        var first = Steps(ref reader, ctx);
        reader.EndArray();
        return first;
    }

    // The hops from the reader's place on, each read before the ones after it and made holding the next.
    private code.Hop? Steps(ref global::app.type.item.kind.json.Reader reader, global::app.type.reader.ReadContext ctx)
    {
        if (!reader.NextElement()) return null;
        var step = Step(ref reader, ctx);
        return step(Steps(ref reader, ctx));
    }

    // One hop as the .pr writes it, made once its next is.
    private System.Func<code.Hop?, code.Hop> Step(ref global::app.type.item.kind.json.Reader reader, global::app.type.reader.ReadContext ctx)
    {
        reader.BeginObject();
        if (!reader.NextName(out var kind))
            throw new global::app.error.PrFormatOutdatedException("a hop in the .pr names its kind", ctx.Origin);
        System.Func<code.Hop?, code.Hop> hop;
        var types = ctx.Context!.App.type.list;
        switch (kind)
        {
            case "variable":
            {
                var name = reader.String();
                hop = next => new code.Variable(name, next, types[typeof(code.Variable)]);
                break;
            }
            case "property":
            {
                var name = reader.String();
                hop = next => new code.Property(name.StartsWith('!') ? name : "." + name, name, next, types[typeof(code.Property)]);
                break;
            }
            case "index":
            {
                var key = Key(ref reader, ctx);
                hop = next => new code.Index($"[{(key is variable.@this v ? v.Text : key.ToString())}]", key, next, types[typeof(code.Index)]);
                break;
            }
            case "method":
            {
                var name = reader.String();
                var values = new List<global::app.type.item.@this>();
                if (!reader.NextName(out var parameter) || parameter != "parameter")
                    throw new global::app.error.PrFormatOutdatedException("a method in the .pr lists its parameter", ctx.Origin);
                reader.BeginArray();
                var rows = new global::app.data.reader.@this();
                while (reader.NextElement()) values.Add(rows.Read(ref reader, ctx).Peek());
                reader.EndArray();
                var list = new global::app.type.item.list.@this(values, types["list"]);
                hop = next => new code.Method($".{name}(…)", name, list, next, types[typeof(code.Method)]);
                break;
            }
            default:
                throw new global::app.error.PrFormatOutdatedException($"hop kind '{kind}' isn't in this .pr format", ctx.Origin);
        }
        if (reader.NextName(out var extra))
            throw new global::app.error.PrFormatOutdatedException($"hop key '{extra}' isn't in this .pr format", ctx.Origin);
        reader.EndObject();
        return hop;
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
            "text" => new global::app.type.item.text.@this(reader.String(), ctx.Context.App.type.list[typeof(global::app.type.item.text.@this)]),
            "variable" => Read(ref reader, ctx).Single(),
            _ => throw new global::app.error.PrFormatOutdatedException($"index key '{kind}' isn't in this .pr format", ctx.Origin),
        };
        if (reader.NextName(out var extra))
            throw new global::app.error.PrFormatOutdatedException($"index key '{extra}' isn't in this .pr format", ctx.Origin);
        reader.EndObject();
        return key;
    }
}
