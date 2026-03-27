using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.variable;

[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    [VariableName]
    public partial string Name { get; init; }
    public partial object? Value { get; init; }
    public partial string? Type { get; init; }

    public Task<Data> Run()
    {
        Context.MemoryStack.Set(Name, Value,
            Type != null ? PLang.Runtime2.Engine.Memory.Type.FromName(Type) : null);
        return Task.FromResult(Context.MemoryStack.Get(Name) ?? Data.Ok());
    }
}
