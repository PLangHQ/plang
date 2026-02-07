using PLang.Runtime2.Core;

namespace PLang.Runtime2.Modules.variable;

public record get
{
    public virtual string name { get; init; } = null!;
}

public sealed partial class GetHandler : BaseClass<get>
{
    protected override Task<Return> ExecuteAsync(get? p)
    {
        if (p == null || string.IsNullOrEmpty(p.name))
            return ErrorTask("Variable name is required", "MissingName");

        var value = MemoryStack.Get(p.name);

        return SuccessTask(value?.Value);
    }
}
