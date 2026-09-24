using app.variable;

namespace app.module.action.list;

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
        var op = (global::app.module.action.condition.Operator)(await Operator.Value())!;

        foreach (var (_, item) in await data.EnumerateItems())
        {
            // The first match — or an error — is the answer; a miss moves on to the next item.
            var matched = await op.Evaluate(await item.Get(key), right, Context);
            if (!matched.Success || matched.ToBoolean()) return matched;
        }

        return Context.Ok<global::app.type.item.@bool.@this>(false, Context.App.Type["bool"]);
    }
}
