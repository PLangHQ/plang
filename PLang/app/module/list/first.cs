using app.variable;

namespace app.module.list;

[Action("first")]
public partial class First : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public Task<data.@this> Run()
    {
        var data = Context.Variable.Get(ListName.Value);
        var first = data.GetChild("[0]");

        return Task.FromResult(first.IsInitialized ? global::app.data.@this.Ok(first.Value) : global::app.data.@this.Ok());
    }
}
