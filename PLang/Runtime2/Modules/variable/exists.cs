using PLang.Runtime2.Core;

namespace PLang.Runtime2.Modules.variable;

public record exists
{
    public virtual string name { get; init; } = null!;
}

public sealed partial class ExistsHandler : BaseClass<exists>
{
    protected override Task<Return> ExecuteAsync(exists? p)
    {
        if (p == null || string.IsNullOrEmpty(p.name))
            return ErrorTask("Variable name is required", "MissingName");

        var result = MemoryStack.Contains(p.name);

        return SuccessTask(result);
    }
}
