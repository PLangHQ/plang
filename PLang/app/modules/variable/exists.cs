using app.variables;

namespace app.modules.variable;

[System.ComponentModel.Description("Return true if a named variable exists and has been initialized in the current scope")]
[Action("exists")]
public partial class Exists : IContext
{
    public partial data.@this<Variable> Name { get; init; }

    public Task<data.@this<bool>> Run()
    {
        return Task.FromResult(global::app.data.@this<bool>.Ok(Context.Variables.Contains(Name.Value)));
    }
}
