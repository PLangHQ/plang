using App.Variables;

namespace App.modules.variable;

/// <summary>
/// Sets a variable in the current context's variable store.
/// When AsDefault is true, only sets if the variable doesn't already exist.
/// </summary>
[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    [VariableName]
    public partial string Name { get; init; }
    public partial Data.@this Value { get; init; }
    public partial string? Type { get; init; }
    [Default(false)]
    public partial bool AsDefault { get; init; }

    public Task<Data.@this> Run()
    {
        if (AsDefault)
        {
            var existing = Context.Variables.Get(Name);
            if (existing.IsInitialized)
                return Task.FromResult(Data());
        }

        Value.Name = Name;
        if (Type != null) Value.Type = App.Data.Type.FromName(Type);
        Context.Variables.Put(Value);

        return Task.FromResult(Data());
    }
}
