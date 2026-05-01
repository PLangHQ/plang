using App.Variables;

namespace App.modules.variable;

[System.ComponentModel.Description("Delete a named variable from the current scope")]
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial Data.@this<Variable> Name { get; init; }

    public Task<Data.@this> Run()
    {
        Context.Variables.Remove(Name.Value);
        return Task.FromResult(Data());
    }
}
