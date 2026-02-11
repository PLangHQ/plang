using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.variable;

[Action("get")]
public partial class Get : IContext
{
    [VariableName]
    public partial string Name { get; init; }

    public Task<Data> Run()
    {
        var data = Context.MemoryStack.Get(Name);
        return Task.FromResult(Data.Ok(
            new types.variable
            {
                name = Name,
                value = data?.Value,
                type = data?.Type?.Value,
                exists = data != null
            }));
    }
}
