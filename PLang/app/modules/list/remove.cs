using app.variables;

namespace app.modules.list;

[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial data.@this<Variable> ListName { get; init; }
    public partial data.@this Value { get; init; }
    [Default(-1)]
    public partial data.@this<int> AtIndex { get; init; }

    public Task<data.@this<types.list>> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        if (data.Value is not List<object?> list)
            return Task.FromResult(global::app.data.@this<types.list>.FromError(
                new app.error.ValidationError($"Variable '{ListName.Value}' is not a list")));

        if (AtIndex.Value >= 0)
        {
            if (AtIndex.Value < list.Count)
                list.RemoveAt(AtIndex.Value);
        }
        else
        {
            list.Remove(Value.Value);
        }

        return Task.FromResult(global::app.data.@this<types.list>.Ok(new types.list { count = list.Count, value = list }, app.data.type.FromName("list")));
    }
}
