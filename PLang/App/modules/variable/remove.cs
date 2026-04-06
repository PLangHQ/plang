using App.Variables;

namespace App.modules.variable;

[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    [VariableName]
    public partial string Name { get; init; }

    public Task<Data.@this> Run()
    {
        Context.Variables.Remove(Name);
        return Task.FromResult(App.Data.@this.Ok());
    }
}
