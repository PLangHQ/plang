using app.variable;

namespace app.modules.list;

[Action("unique")]
public partial class Unique : IContext
{
    public partial data.@this<Variable> ListName { get; init; }

    public Task<data.@this<types.list>> Run()
    {
        var existing = Context.Variables.Get(ListName.Value).Value;
        if (existing is not List<object?> list)
            return Task.FromResult(global::app.data.@this<types.list>.FromError(
                new app.error.ValidationError($"Variable '{ListName.Value}' is not a list")));

        var distinct = list.Distinct().Cast<object?>().ToList();
        return Task.FromResult(global::app.data.@this<types.list>.Ok(
            new types.list { count = distinct.Count, value = distinct }, app.data.type.FromName("list")));
    }
}
