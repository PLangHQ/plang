namespace app.module.action.variable;

[Action("exists")]
public partial class Exists : IContext
{
    public partial data.@this<app.type.item.variable.@this> Name { get; init; }

    public async Task<data.@this<global::app.type.item.@bool.@this>> Start()
    {
        return Context.Ok<global::app.type.item.@bool.@this>(Context.Variable.Contains(await Name.Value()));
    }
}
