using App.Variables;

namespace App.modules.variable;

/// <summary>
/// Retrieves a variable from the current context's variable store.
/// </summary>
[System.ComponentModel.Description("Retrieve a variable's current value from the active scope")]
[Action("get")]
public partial class Get : IContext
{
    [VariableName]
    public partial string Name { get; init; }

    public Task<Data.@this> Run()
    {
        return Task.FromResult(Context.Variables.Get(Name));
    }
}
