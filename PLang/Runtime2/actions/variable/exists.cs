using PLang.Runtime2.Memory;

namespace PLang.Runtime2.actions.variable;

public record exists
{
    public virtual string name { get; init; } = null!;
}

public sealed partial class ExistsHandler : BaseClass<exists>
{
    protected override Task<Data> ExecuteAsync(exists p)
    {
        return SuccessTask(new types.variable { name = p.name, exists = MemoryStack.Contains(p.name) });
    }
}
