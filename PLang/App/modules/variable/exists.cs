using App.Variables;

namespace App.modules.variable;

[Action("exists")]
public partial class Exists : IContext
{
    [VariableName]
    public partial string Name { get; init; }

    public Task<Data> Run()
    {
        return Task.FromResult(Data.Ok(Context.Variables.Contains(Name)));
    }
}
