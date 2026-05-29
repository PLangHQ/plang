using app.variable;

namespace app.modules.variable;

[Action("clear", Cacheable = false)]
public partial class Clear : IContext
{
    public Task<data.@this> Run()
    {
        Context.Variables.Clear();
        return Task.FromResult(Data());
    }
}
