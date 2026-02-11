using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.variable;

[Action("set")]
public partial class Set : IContext
{
    [VariableName]
    public partial string Name { get; init; }
    public partial object? Value { get; init; }
    public partial string? Type { get; init; }

    public Task<Data> Run()
    {
        Context.MemoryStack.Set(Name, Value,
            Type != null ? Memory.Type.FromName(Type) : null);
        return Task.FromResult(Data.Ok(
            new types.variable { name = Name, value = Value, type = Type }));
    }
}
