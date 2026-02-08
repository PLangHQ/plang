using PLang.Runtime2.Memory;

namespace PLang.Runtime2.actions.variable;

public record set
{
    public virtual string name { get; init; } = null!;
    public virtual object? value { get; init; }
    public virtual string? type { get; init; }
}

public sealed partial class SetHandler : BaseClass<set>
{
    protected override Task<Data> ExecuteAsync(set p)
    {
        MemoryStack.Set(p.name, p.value, p.type != null ? Memory.Type.FromName(p.type) : null);
        return SuccessTask(new types.variable { name = p.name, value = p.value, type = p.type });
    }
}
