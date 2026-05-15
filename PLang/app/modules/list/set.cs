using app.Variables;

namespace app.modules.list;

[System.ComponentModel.Description("Replace the item at a zero-based Index in the list with Value")]
[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    public partial data.@this<Variable> ListName { get; init; }
    public partial data.@this<int> Index { get; init; }
    public partial data.@this Value { get; init; }

    public Task<data.@this> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        if (data.Value is not List<object?> list)
            return Task.FromResult(Error(
                new app.Errors.ValidationError($"Variable '{ListName.Value}' is not a list")));

        if (Index.Value < 0 || Index.Value >= list.Count)
            return Task.FromResult(Error(
                new app.Errors.ValidationError($"Index {Index.Value} out of range (0..{list.Count - 1})")));

        list[Index.Value] = Value?.Value;
        return Task.FromResult(Data(new types.list { count = list.Count, value = list }, app.data.type.FromName("list")));
    }
}
