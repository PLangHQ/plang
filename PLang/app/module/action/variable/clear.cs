using app.variable;

namespace app.module.action.variable;

[Action("clear", Cacheable = false)]
public partial class Clear : IContext
{
    public Task<data.@this> Run()
    {
        Context.Variable.Clear();
        return Task.FromResult(Data());
    }
}
