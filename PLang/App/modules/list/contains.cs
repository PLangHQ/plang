namespace App.modules.list;

[System.ComponentModel.Description("Return true if the list contains an item equal to Value")]
[Action("contains")]
public partial class Contains : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    public partial Data.@this Value { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName);
        var target = Value.Value;

        foreach (var (_, item) in data.EnumerateItems())
        {
            if (Equals(item.Value, target))
                return Task.FromResult(Data(true));
        }

        return Task.FromResult(Data(false));
    }
}
