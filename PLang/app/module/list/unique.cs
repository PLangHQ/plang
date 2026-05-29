using app.variable;

namespace app.module.list;

[Action("unique")]
public partial class Unique : IContext
{
    public partial data.@this<Variable> ListName { get; init; }

    public Task<data.@this<type.list>> Run()
    {
        var existing = Context.Variables.Get(ListName.Value).Value;
        if (existing is not List<object?> list)
            return Task.FromResult(global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError($"Variable '{ListName.Value}' is not a list")));

        var distinct = list.Distinct().Cast<object?>().ToList();
        return Task.FromResult(global::app.data.@this<type.list>.Ok(
            new type.list { count = distinct.Count, value = distinct }, app.type.@this.FromName("list")));
    }
}
