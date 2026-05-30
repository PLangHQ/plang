using app.variable;

namespace app.module.list;

[Action("reverse", Cacheable = false)]
public partial class Reverse : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public Task<data.@this<type.list>> Run()
    {
        var data = Context.Variable.Get(ListName.Value);
        if (data.Value is not List<object?> list)
            return Task.FromResult(global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError($"Variable '{ListName.Value}' is not a list")));

        list.Reverse();
        return Task.FromResult(global::app.data.@this<type.list>.Ok(new type.list { count = list.Count, value = list }, app.type.@this.FromName("list")));
    }
}
