using app.variables;

namespace app.modules.variable;

[Action("exists")]
public partial class Exists : IContext
{
    public partial data.@this<Variable> Name { get; init; }

    public Task<data.@this<bool>> Run()
    {
        return Task.FromResult(global::app.data.@this<bool>.Ok(Context.Variables.Contains(Name.Value)));
    }
}
