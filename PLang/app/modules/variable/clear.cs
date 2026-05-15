using app.Variables;

namespace app.modules.variable;

[ModuleDescription("Read, write, and inspect PLang runtime variables in the current scope")]
[System.ComponentModel.Description("Remove all variables from the current scope")]
[Action("clear", Cacheable = false)]
public partial class Clear : IContext
{
    public Task<Data.@this> Run()
    {
        Context.Variables.Clear();
        return Task.FromResult(Data());
    }
}
