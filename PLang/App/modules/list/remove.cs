using App.Variables;

namespace App.modules.list;

[System.ComponentModel.Description("Remove an item from the list by value or by zero-based index")]
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial Data.@this<Variable> ListName { get; init; }
    public partial Data.@this Value { get; init; }
    [Default(-1)]
    public partial Data.@this<int> AtIndex { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        if (data.Value is not List<object?> list)
            return Task.FromResult(Error(
                new App.Errors.ValidationError($"Variable '{ListName.Value}' is not a list")));

        if (AtIndex.Value >= 0)
        {
            if (AtIndex.Value < list.Count)
                list.RemoveAt(AtIndex.Value);
        }
        else
        {
            list.Remove(Value.Value);
        }

        return Task.FromResult(Data(new types.list { count = list.Count, value = list }, App.Data.Type.FromName("list")));
    }
}
