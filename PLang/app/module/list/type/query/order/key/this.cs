using Data = global::app.data.@this;
using List = global::app.type.item.list.@this;

namespace app.module.list.type.query.order.key;

/// <summary>
/// One key an order sorts by: a field, or the elements themselves when it names none, ascending unless
/// <c>desc</c>. Written as a field (<c>"age"</c>, or a %variable% holding one) or <c>{field, desc}</c>.
/// </summary>
public sealed class @this
{
    // the field as written — read where the query runs (a %variable% holding the field's name)
    private readonly Data? _field;
    private readonly global::app.type.item.@bool.@this _desc;

    private @this(Data? field, global::app.type.item.@bool.@this desc)
    {
        _field = field;
        _desc = desc;
    }

    /// <summary>What <paramref name="key"/> is; one that is neither a field nor <c>{field, desc}</c>, or whose desc
    /// doesn't read as a bool, is why on <c>data</c>.</summary>
    internal static @this? Create(Data key, Data data, global::app.actor.context.@this context)
    {
        if (key.Peek() is global::app.type.item.text.@this text && text.ToString().Length > 0)
            return new(key, false);
        if (key.Peek() is not global::app.type.item.dict.@this dict)
        {
            data.Fail(new global::app.error.Error("a key is a field (\"age\") or {field, desc}", "QueryInvalid", 400));
            return null;
        }
        var field = dict.Get("field", context) is { } named && named.Peek()?.ToString() is { Length: > 0 } ? named : null;
        if (dict.Get("desc", context) is not { } desc) return new(field, false);
        return global::app.type.item.@bool.@this.Create(desc.Peek(), null, data) is { } descending ? new(field, descending) : null;
    }

    /// <summary><paramref name="rows"/> sorted by this key — a new list (<c>list.Sort</c>). The field is read here,
    /// where the query runs: a %variable% not set fails (VariableNotFound).</summary>
    internal async System.Threading.Tasks.Task<Data> Sort(List rows, global::app.actor.context.@this context)
    {
        global::app.type.item.text.@this? by = null;
        if (_field != null)
        {
            var read = await _field.Settle();
            if (!read.Success) return read;
            by = (await read.Value())?.ToString() is { Length: > 0 } name ? new global::app.type.item.text.@this(name) : null;
        }
        return await rows.Sort(by, _desc, context);
    }

    /// <summary>Writes the key as <c>{field, desc}</c>, its field as written; a key with no field writes only its
    /// desc.</summary>
    internal void Output(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        if (_field != null) { writer.Name("field"); writer.String(_field.Peek().ToString() ?? ""); }
        writer.Name("desc"); _desc.Write(writer);
        writer.EndObject();
    }
}
