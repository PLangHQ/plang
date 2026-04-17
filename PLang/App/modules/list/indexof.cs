namespace App.modules.list;

[Action("indexof")]
public partial class IndexOf : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    public partial Data.@this Value { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName);
        var target = Value.Value;

        foreach (var (key, item) in data.EnumerateItems())
        {
            if (Equals(item.Value, target))
                return Task.FromResult(Data(key.Value));
        }

        return Task.FromResult(Data(-1));
    }
}
