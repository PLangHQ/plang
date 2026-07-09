using app.variable;

namespace app.module.list;

[Action("first")]
public partial class First : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public async Task<data.@this> Run()
    {
        var data = await Context.Variable.Get((await ListName.Value()));
        var first = await data.Get("[0]");

        return first.IsInitialized ? Context.Ok((await first.Value())) : Context.Ok();
    }
}
