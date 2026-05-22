using app.variables;

namespace app.modules.list;

/// <summary>
/// Checks if any item in a list matches a condition on a property.
/// Usage: any %list% where "level" != "high", write to %hasNonHigh%
/// </summary>
[System.ComponentModel.Description("Return true if any item in the list has a property matching the given operator and value")]
[Action("any")]
public partial class Any : IContext
{
    public partial data.@this<Variable> ListName { get; init; }
    [IsNotNull]
    public partial data.@this<string> Key { get; init; }
    [IsNotNull]
    public partial data.@this<condition.Operator> Operator { get; init; }
    public partial data.@this Value { get; init; }

    public async Task<data.@this> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        var key = Key.Value!;
        var right = Value.Value != null ? new data.@this("", Value.Value) : null;

        foreach (var (_, item) in data.EnumerateItems())
        {
            var left = item.GetChild(key);
            if (await Operator.Value!.Evaluate(left, right))
                return Data(true, app.data.type.FromName("bool"));
        }

        return Data(false, app.data.type.FromName("bool"));
    }
}
