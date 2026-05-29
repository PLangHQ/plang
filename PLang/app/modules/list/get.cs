using app.variable;

namespace app.modules.list;

[Action("get")]
public partial class Get : IContext
{
    public partial data.@this<Variable> ListName { get; init; }
    public partial data.@this<int> Index { get; init; }

    public Task<data.@this<object>> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        var item = data.GetChild($"[{Index.Value}]");

        if (!item.IsInitialized)
            return Task.FromResult(global::app.data.@this<object>.FromError(
                new app.error.ValidationError($"Index {Index.Value} out of range for '{ListName.Value}'")));

        return Task.FromResult(global::app.data.@this<object>.Ok(item.Value));
    }
}
