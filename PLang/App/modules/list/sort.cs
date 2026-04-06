using App.Engine.Variables;

namespace App.modules.list;

[Action("sort", Cacheable = false)]
public partial class Sort : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    [Default(false)]
    public partial bool Descending { get; init; }

    public Task<Data> Run()
    {
        var data = Context.Variables.Get(ListName);
        if (data?.Value is not List<object?> list)
            return Task.FromResult(Data.FromError(
                new App.Engine.Errors.ValidationError($"Variable '{ListName}' is not a list")));

        if (Descending)
            list.Sort((a, b) => Comparer<object>.Default.Compare(b, a));
        else
            list.Sort((a, b) => Comparer<object>.Default.Compare(a, b));

        return Task.FromResult(Data.Ok(new types.list { count = list.Count, value = list }, App.Engine.Variables.Type.FromName("list")));
    }
}
