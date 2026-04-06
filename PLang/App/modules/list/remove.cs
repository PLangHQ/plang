using App.Variables;

namespace App.modules.list;

[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    public partial object? Value { get; init; }
    [Default(-1)]
    public partial int AtIndex { get; init; }

    public Task<Data> Run()
    {
        var data = Context.Variables.Get(ListName);
        if (data?.Value is not List<object?> list)
            return Task.FromResult(Data.FromError(
                new App.Errors.ValidationError($"Variable '{ListName}' is not a list")));

        if (AtIndex >= 0)
        {
            if (AtIndex < list.Count)
                list.RemoveAt(AtIndex);
        }
        else
        {
            list.Remove(Value);
        }

        return Task.FromResult(Data.Ok(new types.list { count = list.Count, value = list }, App.Variables.Type.FromName("list")));
    }
}
