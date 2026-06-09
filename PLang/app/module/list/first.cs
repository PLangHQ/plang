using app.variable;

namespace app.module.list;

[Action("first")]
public partial class First : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public async Task<data.@this> Run()
    {
        var data = Context.Variable.Get((await ListName.Value()));
        var first = data.GetChild("[0]");

        return first.IsInitialized ? global::app.data.@this.Ok((await first.Value())) : global::app.data.@this.Ok();
    }
}
