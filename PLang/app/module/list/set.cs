using app.variable;

namespace app.module.list;

[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    public partial data.@this<int> Index { get; init; }
    public partial data.@this Value { get; init; }

    public Task<data.@this<type.list>> Run()
    {
        var data = Context.Variable.Get(ListName.Value);
        if (data.Value is app.type.list.@this nl)
        {
            if (Index.Value < 0 || Index.Value >= nl.Count)
                return Task.FromResult(global::app.data.@this<type.list>.FromError(
                    new app.error.ValidationError($"Index {Index.Value} out of range (0..{nl.Count - 1})")));
            nl.SetAt(Index.Value, Value ?? new global::app.data.@this("", null));
            return Task.FromResult(global::app.data.@this<type.list>.Ok(new type.list { count = nl.Count, value = nl }, app.type.@this.FromName("list")));
        }
        if (data.Value is not List<object?> list)
            return Task.FromResult(global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError($"Variable '{ListName.Value}' is not a list")));

        if (Index.Value < 0 || Index.Value >= list.Count)
            return Task.FromResult(global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError($"Index {Index.Value} out of range (0..{list.Count - 1})")));

        list[Index.Value] = Value?.Value;
        return Task.FromResult(global::app.data.@this<type.list>.Ok(new type.list { count = list.Count, value = list }, app.type.@this.FromName("list")));
    }
}
