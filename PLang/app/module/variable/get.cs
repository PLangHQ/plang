using app.variable;

namespace app.module.variable;

/// <summary>
/// Retrieves a variable from the current context's variable store.
/// </summary>
[Action("get")]
public partial class Get : IContext
{
    public partial data.@this<Variable> Name { get; init; }

    public Task<data.@this> Run()
    {
        return Task.FromResult(Context.Variable.Get(Name.Value));
    }
}
