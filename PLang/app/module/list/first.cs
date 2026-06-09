using app.variable;

namespace app.module.list;

[Action("first")]
public partial class First : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public Task<data.@this> Run()
    {
        var data = Context.Variable.Get(ListName.Materialize() as app.variable.@this);
        var first = data.GetChild("[0]");

        return Task.FromResult(first.IsInitialized ? global::app.data.@this.Ok(first.Materialize()) : global::app.data.@this.Ok());
    }
}
