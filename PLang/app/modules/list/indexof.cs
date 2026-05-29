using app.variable;

namespace app.modules.list;

[Action("indexof")]
public partial class IndexOf : IContext
{
    public partial data.@this<Variable> ListName { get; init; }
    public partial data.@this Value { get; init; }

    public Task<data.@this<int>> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        var target = Value.Value;

        foreach (var (key, item) in data.EnumerateItems())
        {
            if (Equals(item.Value, target))
                return Task.FromResult(global::app.data.@this<int>.Ok(Convert.ToInt32(key.Value)));
        }

        return Task.FromResult(global::app.data.@this<int>.Ok(-1));
    }
}
