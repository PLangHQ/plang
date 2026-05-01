using App.Variables;

namespace App.modules.list;

/// <summary>
/// Checks if any item in a list matches a condition on a property.
/// Usage: any %list% where "level" != "high", write to %hasNonHigh%
/// </summary>
[System.ComponentModel.Description("Return true if any item in the list has a property matching the given operator and value")]
[Action("any")]
public partial class Any : IContext
{
    public partial Data.@this<Variable> ListName { get; init; }
    [IsNotNull]
    public partial Data.@this<string> Key { get; init; }
    [IsNotNull]
    public partial Data.@this<condition.Operator> Operator { get; init; }
    public partial Data.@this Value { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        var key = Key.Value!;
        var right = Value.Value != null ? new Data.@this("", Value.Value) : null;

        foreach (var (_, item) in data.EnumerateItems())
        {
            var left = item.GetChild(key);
            if (Operator.Value!.Evaluate(left, right))
                return Task.FromResult(Data(true, App.Data.Type.FromName("bool")));
        }

        return Task.FromResult(Data(false, App.Data.Type.FromName("bool")));
    }
}
