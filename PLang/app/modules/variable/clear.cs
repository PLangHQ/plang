using app.variables;

namespace app.modules.variable;

[ModuleDescription("Read, write, and inspect PLang runtime variables in the current scope")]
[System.ComponentModel.Description("Remove all variables from the current scope")]
[Action("clear", Cacheable = false)]
public partial class Clear : IContext
{
    public Task<data.@this> Run()
    {
        Context.Variables.Clear();
        return Task.FromResult(Data());
    }
}
