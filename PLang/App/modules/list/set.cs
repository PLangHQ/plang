using App.Variables;

namespace App.modules.list;

[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    public partial Data.@this<int> Index { get; init; }
    public partial Data.@this Value { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName);
        if (data.Value is not List<object?> list)
            return Task.FromResult(Error(
                new App.Errors.ValidationError($"Variable '{ListName}' is not a list")));

        if (Index.Value < 0 || Index.Value >= list.Count)
            return Task.FromResult(Error(
                new App.Errors.ValidationError($"Index {Index.Value} out of range (0..{list.Count - 1})")));

        list[Index.Value] = Value?.Value;
        return Task.FromResult(Data(new types.list { count = list.Count, value = list }, App.Data.Type.FromName("list")));
    }
}
