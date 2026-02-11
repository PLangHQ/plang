using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.variable;

[Action("remove")]
public partial class Remove : IContext
{
    [VariableName]
    public partial string Name { get; init; }

    public Task<Data> Run()
    {
        var removed = Context.MemoryStack.Remove(Name);
        return Task.FromResult(Data.Ok(
            new types.variable { name = Name, exists = removed }));
    }
}
