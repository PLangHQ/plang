using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.variable;

[Action("exists")]
public partial class Exists : IContext
{
    [VariableName]
    public partial string Name { get; init; }

    public Task<Data> Run()
    {
        return Task.FromResult(Data.Ok(
            new types.variable { name = Name, exists = Context.MemoryStack.Contains(Name) }));
    }
}
