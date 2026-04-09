using App.Variables;

namespace App.modules.variable;

[Action("clear", Cacheable = false)]
public partial class Clear : IContext
{
    public Task<Data.@this> Run()
    {
        Context.Variables.Clear();
        return Task.FromResult(Data());
    }
}
