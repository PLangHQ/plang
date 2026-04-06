using App.Variables;

namespace App.modules.list;

[Action("unique")]
public partial class Unique : IContext
{
    [VariableName]
    public partial string ListName { get; init; }

    public Task<Data.@this> Run()
    {
        var existing = Context.Variables.Get(ListName)?.Value;
        if (existing is not List<object?> list)
            return Task.FromResult(Data.@this.FromError(
                new App.Errors.ValidationError($"Variable '{ListName}' is not a list")));

        var distinct = list.Distinct().Cast<object?>().ToList();
        return Task.FromResult(Data.@this.Ok(distinct, App.Data.Type.FromName("list")));
    }
}
