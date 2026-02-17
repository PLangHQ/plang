using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.variable;

[Action("get")]
public partial class Get : IContext
{
    [VariableName]
    public partial string Name { get; init; }

    public Task<Data> Run()
    {
        var data = Context.MemoryStack.Get(Name);
        if (data == null || !data.IsInitialized)
            return Task.FromResult(Data.Ok(null));

        return Task.FromResult(new Data(Name, data.Value, data.Type));
    }
}
