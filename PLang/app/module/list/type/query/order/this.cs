using Data = global::app.data.@this;
using List = global::app.type.item.list.@this;

namespace app.module.list.type.query.order;

/// <summary>
/// The order part: the rows sorted by its keys, the first key first (<c>list.Sort</c>). Written as a field
/// (<c>order: "age"</c>), one key (<c>{field: "age", desc: true}</c>) or a list of keys; a key with no field
/// orders by the elements themselves.
/// </summary>
public sealed class @this : part.@this
{
    private readonly IReadOnlyList<(global::app.type.item.text.@this? Field, bool Desc)> _key;

    private @this(IReadOnlyList<(global::app.type.item.text.@this? Field, bool Desc)> key) => _key = key;

    /// <summary>The order of <paramref name="written"/>: its keys, in the order they decide; a key that doesn't
    /// read is why on <c>data</c>.</summary>
    internal static @this? Create(Data written, Data data, global::app.actor.context.@this context)
    {
        List<object?> each = written.Peek() is List keys ? keys.Items(context).Select(k => (object?)k.Peek()).ToList() : [written.Peek()];
        var key = new List<(global::app.type.item.text.@this? Field, bool Desc)>();
        foreach (var one in each)
        {
            if (Key(one, context) is not { } read)
            {
                data.Fail(new global::app.error.Error("order: a key is a field (\"age\") or {field, desc}", "QueryInvalid", 400));
                return null;
            }
            key.Add(read);
        }
        return new(key);
    }

    internal override int Rank => 3;

    internal override async System.Threading.Tasks.Task<Data> Apply(List rows, IReadOnlyList<part.@this> rest,
        global::app.actor.context.@this context)
    {
        // the last key sorts first: the sort keeps the order of equal keys, so each key before it decides over it
        Data sorted = context.Ok(rows);
        foreach (var (field, desc) in _key.Reverse())
        {
            sorted = await sorted.Use<List>(left => left.Sort(field, desc, context));
            if (!sorted.Success) break;
        }
        return await Next(sorted, rest, context);
    }

    internal override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this context)
    {
        writer.BeginArray(_key.Count);
        foreach (var (field, desc) in _key)
        {
            writer.BeginObject();
            if (field != null) { writer.Name("field"); writer.String(field.ToString()); }
            writer.Name("desc"); writer.Bool(desc);
            writer.EndObject();
        }
        writer.EndArray();
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }

    // One key: a field, or {field?, desc?}; null when it is neither.
    private static (global::app.type.item.text.@this? Field, bool Desc)? Key(object? written, global::app.actor.context.@this context)
    {
        if (written is global::app.type.item.dict.@this dict)
        {
            global::app.type.item.text.@this? field = dict.Get("field", context)?.Peek()?.ToString() is { Length: > 0 } name
                ? new global::app.type.item.text.@this(name) : null;
            return (field, dict.Get("desc", context)?.Peek() is global::app.type.item.@bool.@this { Value: true });
        }
        if (written is global::app.type.item.text.@this { } text && text.ToString().Length > 0) return (text, false);
        return null;
    }
}
