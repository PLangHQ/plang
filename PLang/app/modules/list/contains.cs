using app.variables;

namespace app.modules.list;

[System.ComponentModel.Description("Return true if the list contains an item equal to Value")]
[Action("contains")]
public partial class Contains : IContext
{
    public partial data.@this<Variable> ListName { get; init; }
    public partial data.@this Value { get; init; }

    public Task<data.@this<bool>> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        var target = Value.Value;

        foreach (var (_, item) in data.EnumerateItems())
        {
            if (Equals(item.Value, target))
                return Task.FromResult(global::app.data.@this<bool>.Ok(true));
        }

        return Task.FromResult(global::app.data.@this<bool>.Ok(false));
    }
}
