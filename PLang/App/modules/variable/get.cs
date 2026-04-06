using App.Variables;

namespace App.modules.variable;

[Action("get")]
public partial class Get : IContext
{
    [VariableName]
    public partial string Name { get; init; }

    public Task<Data.@this> Run()
    {
        return Task.FromResult(Context.Variables.Get(Name) ?? Data.@this.Ok(null));
    }
}
