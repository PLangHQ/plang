using App.Variables;

namespace App.modules.variable;

[Action("exists")]
public partial class Exists : IContext
{
    [VariableName]
    public partial string Name { get; init; }

    public Task<Data.@this> Run()
    {
        return Task.FromResult(Data(Context.Variables.Contains(Name)));
    }
}
