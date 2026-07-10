using app.variable;

namespace app.module.list;

/// <summary>
/// Checks if any item in a list matches a condition on a property.
/// Usage: any %list% where "level" != "high", write to %hasNonHigh%
/// </summary>
[Action("any")]
public partial class Any : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    [IsNotNull]
    public partial data.@this<global::app.type.item.text.@this> Key { get; init; }
    [IsNotNull]
    public partial data.@this<global::app.type.item.choice.@this<condition.Operator>> Operator { get; init; }
    public partial data.@this Value { get; init; }

    public async Task<data.@this<global::app.type.item.@bool.@this>> Run()
    {
        var data = await Context.Variable.Get(await ListName.Value());
        var key = (await Key.Value())!.Clr<string>()!;
        var rightVal = await Value.Value();
        var right = rightVal != null ? new data.@this("", rightVal, context: Context) : null;
        var op = (global::app.module.condition.Operator)(await Operator.Value())!;

        foreach (var (_, item) in await data.EnumerateItems())
        {
            var left = await item.Get(key);
            if (await op.Evaluate(left, right))
                return Context.Ok<global::app.type.item.@bool.@this>(true, Context.Type.Create("bool"));
        }

        return Context.Ok<global::app.type.item.@bool.@this>(false, Context.Type.Create("bool"));
    }
}
