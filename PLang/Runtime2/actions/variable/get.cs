using PLang.Runtime2.Memory;

namespace PLang.Runtime2.actions.variable;

public record get
{
    public virtual string name { get; init; } = null!;
}

public sealed partial class GetHandler : BaseClass<get>
{
    protected override Task<Data> ExecuteAsync(get p)
    {
        var data = MemoryStack.Get(p.name);
        return SuccessTask(new types.variable
        {
            name = p.name,
            value = data?.Value,
            type = data?.Type?.Value,
            exists = data != null
        });
    }
}
