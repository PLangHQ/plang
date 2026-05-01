using App.Variables;

namespace App.modules.list;

[System.ComponentModel.Description("Return the zero-based index of the first item equal to Value, or -1 if not found")]
[Action("indexof")]
public partial class IndexOf : IContext
{
    public partial Data.@this<Variable> ListName { get; init; }
    public partial Data.@this Value { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        var target = Value.Value;

        foreach (var (key, item) in data.EnumerateItems())
        {
            if (Equals(item.Value, target))
                return Task.FromResult(Data(key.Value));
        }

        return Task.FromResult(Data(-1));
    }
}
