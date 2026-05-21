using app.variables;

namespace app.modules.list;

[System.ComponentModel.Description("Remove duplicate items from the list and return the deduplicated result")]
[Action("unique")]
public partial class Unique : IContext
{
    public partial data.@this<Variable> ListName { get; init; }

    public Task<data.@this> Run()
    {
        var existing = Context.Variables.Get(ListName.Value).Value;
        if (existing is not List<object?> list)
            return Task.FromResult(Error(
                new app.errors.ValidationError($"Variable '{ListName.Value}' is not a list")));

        var distinct = list.Distinct().Cast<object?>().ToList();
        return Task.FromResult(Data(distinct, app.data.type.FromName("list")));
    }
}
