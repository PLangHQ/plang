using App.Variables;

namespace App.modules.list;

[Action("sort", Cacheable = false)]
public partial class Sort : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    [Default(false)]
    public partial bool Descending { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName);
        if (data?.Value is not List<object?> list)
            return Task.FromResult(App.Data.@this.FromError(
                new App.Errors.ValidationError($"Variable '{ListName}' is not a list")));

        if (Descending)
            list.Sort((a, b) => Comparer<object>.Default.Compare(b, a));
        else
            list.Sort((a, b) => Comparer<object>.Default.Compare(a, b));

        return Task.FromResult(App.Data.@this.Ok(new types.list { count = list.Count, value = list }, App.Data.Type.FromName("list")));
    }
}
