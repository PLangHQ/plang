using app.variables;

namespace app.modules.list;

[Action("reverse", Cacheable = false)]
public partial class Reverse : IContext
{
    public partial data.@this<Variable> ListName { get; init; }

    public Task<data.@this<types.list>> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        if (data.Value is not List<object?> list)
            return Task.FromResult(global::app.data.@this<types.list>.FromError(
                new app.error.ValidationError($"Variable '{ListName.Value}' is not a list")));

        list.Reverse();
        return Task.FromResult(global::app.data.@this<types.list>.Ok(new types.list { count = list.Count, value = list }, app.data.type.FromName("list")));
    }
}
