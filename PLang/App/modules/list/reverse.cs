using App.Variables;

namespace App.modules.list;

[Action("reverse", Cacheable = false)]
public partial class Reverse : IContext
{
    [VariableName]
    public partial string ListName { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName);
        if (data?.Value is not List<object?> list)
            return Task.FromResult(Data.@this.FromError(
                new App.Errors.ValidationError($"Variable '{ListName}' is not a list")));

        list.Reverse();
        return Task.FromResult(Data.@this.Ok(new types.list { count = list.Count, value = list }, App.Data.Type.FromName("list")));
    }
}
