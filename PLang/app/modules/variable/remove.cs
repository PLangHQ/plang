using app.variables;

namespace app.modules.variable;

[System.ComponentModel.Description("Delete a named variable from the current scope")]
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial data.@this<Variable> Name { get; init; }

    public Task<data.@this> Run()
    {
        Context.Variables.Remove(Name.Value);
        return Task.FromResult(Data());
    }
}
