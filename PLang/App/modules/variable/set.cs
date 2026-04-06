using App.Engine.Variables;

namespace App.modules.variable;

[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    [VariableName]
    public partial string Name { get; init; }
    public partial object? Value { get; init; }
    public partial string? Type { get; init; }
    [Default(false)]
    public partial bool AsDefault { get; init; }

    public Task<Data> Run()
    {
        if (AsDefault)
        {
            var existing = Context.Variables.Get(Name);
            if (existing != null && existing.IsInitialized)
                return Task.FromResult(existing);
        }

        Context.Variables.Set(Name, Value,
            Type != null ? App.Engine.Variables.Type.FromName(Type) : null);
        return Task.FromResult(Context.Variables.Get(Name) ?? Data.Ok());
    }
}
