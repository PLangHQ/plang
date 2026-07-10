using app.variable;

namespace app.module.variable;

[Action("exists")]
public partial class Exists : IContext
{
    public partial data.@this<app.variable.@this> Name { get; init; }

    public async Task<data.@this<global::app.type.item.@bool.@this>> Run()
    {
        return Context.Ok<global::app.type.item.@bool.@this>(Context.Variable.Contains(await Name.Value()));
    }
}
