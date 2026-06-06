using app.variable;

namespace app.module.list;

[Action("get")]
public partial class Get : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    public partial data.@this<global::app.type.number.@this> Index { get; init; }

    public Task<data.@this> Run()
    {
        var data = Context.Variable.Get(ListName.Value);
        var item = data.GetChild($"[{Index.Value}]");

        if (!item.IsInitialized)
            return Task.FromResult(global::app.data.@this.FromError(
                new app.error.ValidationError($"Index {Index.Value} out of range for '{ListName.Value}'")));

        return Task.FromResult(global::app.data.@this.Ok(item.Value));
    }
}
