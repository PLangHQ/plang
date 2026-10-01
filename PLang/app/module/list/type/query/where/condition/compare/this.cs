using Data = global::app.data.@this;
using List = global::app.type.item.list.@this;

namespace app.module.list.type.query.where.condition.compare;

/// <summary>One field compared to a value — <c>{field: "age", op: "&gt;", value: 20}</c>. The rows keep the
/// elements whose field holds (<c>list.Where</c>); a value written as a %variable% is the one read at run.</summary>
public sealed class @this : condition.@this
{
    private readonly global::app.type.item.text.@this _field;
    private readonly global::app.data.Operator _op;
    private readonly Data _value;

    internal @this(global::app.type.item.text.@this field, global::app.data.Operator op, Data value)
    {
        _field = field;
        _op = op;
        _value = value;
    }

    internal override System.Threading.Tasks.Task<Data> Keep(List rows, global::app.actor.context.@this context)
        => rows.Where(_field, _op, _value, context);

    internal override async System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this context)
    {
        writer.BeginObject();
        writer.Name("field"); writer.String(_field.ToString());
        writer.Name("op"); writer.String(_op.Value);
        writer.Name("value"); await _value.Output(writer, mode, context);
        writer.EndObject();
    }
}
