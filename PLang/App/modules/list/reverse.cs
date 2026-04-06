using App.Engine.Variables;

namespace App.modules.list;

[Action("reverse", Cacheable = false)]
public partial class Reverse : IContext
{
    [VariableName]
    public partial string ListName { get; init; }

    public Task<Data> Run()
    {
        var data = Context.Variables.Get(ListName);
        if (data?.Value is not List<object?> list)
            return Task.FromResult(Data.FromError(
                new App.Engine.Errors.ValidationError($"Variable '{ListName}' is not a list")));

        list.Reverse();
        return Task.FromResult(Data.Ok(new types.list { count = list.Count, value = list }, App.Engine.Variables.Type.FromName("list")));
    }
}
