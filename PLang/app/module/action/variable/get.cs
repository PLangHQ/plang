using app.variable;

namespace app.module.action.variable;

/// <summary>
/// Retrieves a variable from the current context's variable store.
/// </summary>
[Action("get")]
public partial class Get : IContext
{
    public partial data.@this<app.variable.@this> Name { get; init; }

    public async Task<data.@this> Run()
    {
        return await Context.Variable.Get(await Name.Value());
    }
}
