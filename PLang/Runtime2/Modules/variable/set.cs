using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Modules.variable;

public record set
{
    public virtual string name { get; init; } = null!;
    public virtual object? value { get; init; }
    public virtual string? type { get; init; }
}

public sealed partial class SetHandler : BaseClass<set>
{
    protected override Task<Return> ExecuteAsync(set? p)
    {
        if (p == null || string.IsNullOrEmpty(p.name))
            return ErrorTask("Invalid parameters for set operation", "InvalidParameters");

        MemoryStack.Set(p.name, p.value, p.type != null ? TypeInfo.FromName(p.type) : null);

        return SuccessTask(p.value);
    }
}
