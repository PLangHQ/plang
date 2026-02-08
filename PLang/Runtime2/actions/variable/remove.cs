using PLang.Runtime2.Memory;

namespace PLang.Runtime2.actions.variable;

public record remove
{
    public virtual string name { get; init; } = null!;
}

public sealed partial class RemoveHandler : BaseClass<remove>
{
    protected override Task<Data> ExecuteAsync(remove p)
    {
        var removed = MemoryStack.Remove(p.name);
        return SuccessTask(new types.variable { name = p.name, exists = removed });
    }
}
