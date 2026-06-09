using app.variable;

namespace app.module.list;

[Action("last")]
public partial class Last : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public Task<data.@this> Run()
    {
        var data = Context.Variable.Get(ListName.Materialize() as app.variable.@this);
        var countData = data.GetChild("Count");

        if (countData.IsInitialized && countData.Materialize() is int count && count > 0)
        {
            var last = data.GetChild($"[{count - 1}]");
            if (last.IsInitialized) return Task.FromResult(global::app.data.@this.Ok(last.Materialize()));
        }

        return Task.FromResult(global::app.data.@this.Ok());
    }
}
