using App.Engine.Variables;

namespace App.modules.variable;

[Action("clear", Cacheable = false)]
public partial class Clear : IContext
{
    public Task<Data> Run()
    {
        Context.Variables.Clear();
        return Task.FromResult(Data.Ok());
    }
}
