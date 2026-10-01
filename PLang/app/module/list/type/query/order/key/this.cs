using Data = global::app.data.@this;
using List = global::app.type.item.list.@this;

namespace app.module.list.type.query.order.key;

/// <summary>
/// One key an order sorts by: a field, or the elements themselves when it names none, ascending unless
/// <c>desc</c>. Written as a field (<c>"age"</c>) or <c>{field, desc}</c>.
/// </summary>
public sealed class @this
{
    private readonly global::app.type.item.text.@this? _field;
    private readonly global::app.type.item.@bool.@this _desc;

    private @this(global::app.type.item.text.@this? field, global::app.type.item.@bool.@this desc)
    {
        _field = field;
        _desc = desc;
    }

    /// <summary>What <paramref name="key"/> is; one that is neither a field nor <c>{field, desc}</c>, or whose desc
    /// doesn't read as a bool, is why on <c>data</c>.</summary>
    internal static @this? Create(Data key, Data data, global::app.actor.context.@this context)
    {
        if (key.Peek() is global::app.type.item.text.@this text && text.ToString().Length > 0)
            return new(text, false);
        if (key.Peek() is not global::app.type.item.dict.@this dict)
        {
            data.Fail(new global::app.error.Error("a key is a field (\"age\") or {field, desc}", "QueryInvalid", 400));
            return null;
        }
        global::app.type.item.text.@this? field = dict.Get("field", context)?.Peek()?.ToString() is { Length: > 0 } name
            ? new global::app.type.item.text.@this(name) : null;
        if (dict.Get("desc", context) is not { } desc) return new(field, false);
        return global::app.type.item.@bool.@this.Create(desc.Peek(), null, data) is { } descending ? new(field, descending) : null;
    }

    /// <summary><paramref name="rows"/> sorted by this key — a new list (<c>list.Sort</c>).</summary>
    internal System.Threading.Tasks.Task<Data> Sort(List rows, global::app.actor.context.@this context)
        => rows.Sort(_field, _desc, context);

    /// <summary>Writes the key as <c>{field, desc}</c>; a key with no field writes only its desc.</summary>
    internal void Output(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        if (_field != null) { writer.Name("field"); writer.String(_field.ToString()); }
        writer.Name("desc"); _desc.Write(writer);
        writer.EndObject();
    }
}
