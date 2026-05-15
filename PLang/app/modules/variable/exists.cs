using app.Variables;

namespace app.modules.variable;

[System.ComponentModel.Description("Return true if a named variable exists and has been initialized in the current scope")]
[Action("exists")]
public partial class Exists : IContext
{
    public partial Data.@this<Variable> Name { get; init; }

    public Task<Data.@this> Run()
    {
        return Task.FromResult(Data(Context.Variables.Contains(Name.Value)));
    }
}
