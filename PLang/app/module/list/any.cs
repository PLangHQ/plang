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
    public partial data.@this<string> Key { get; init; }
    [IsNotNull]
    public partial data.@this<condition.Operator> Operator { get; init; }
    public partial data.@this Value { get; init; }

    public async Task<data.@this<bool>> Run()
    {
        var data = Context.Variable.Get(ListName.Value);
        var key = Key.Value!;
        var right = Value.Value != null ? new data.@this("", Value.Value) : null;

        foreach (var (_, item) in data.EnumerateItems())
        {
            var left = item.GetChild(key);
            if (await Operator.Value!.Evaluate(left, right))
                return global::app.data.@this<bool>.Ok(true, app.type.@this.FromName("bool"));
        }

        return global::app.data.@this<bool>.Ok(false, app.type.@this.FromName("bool"));
    }
}
