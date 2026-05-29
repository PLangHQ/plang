using app.variable;

namespace app.module.list;

[Action("sort", Cacheable = false)]
public partial class Sort : IContext
{
    public partial data.@this<Variable> ListName { get; init; }
    [Default(false)]
    public partial data.@this<bool> Descending { get; init; }

    public Task<data.@this<type.list>> Run()
    {
        var data = Context.Variable.Get(ListName.Value);
        if (data.Value is not List<object?> list)
            return Task.FromResult(global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError($"Variable '{ListName.Value}' is not a list")));

        if (Descending.Value)
            list.Sort((a, b) => Comparer<object>.Default.Compare(b, a));
        else
            list.Sort((a, b) => Comparer<object>.Default.Compare(a, b));

        return Task.FromResult(global::app.data.@this<type.list>.Ok(new type.list { count = list.Count, value = list }, app.type.@this.FromName("list")));
    }
}
