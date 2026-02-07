using PLang.Runtime2.Core;

namespace PLang.Runtime2.Modules.variable;

public record remove
{
    public virtual string name { get; init; } = null!;
}

public sealed partial class RemoveHandler : BaseClass<remove>
{
    protected override Task<Return> ExecuteAsync(remove? p)
    {
        if (p == null || string.IsNullOrEmpty(p.name))
            return ErrorTask("Variable name is required", "MissingName");

        var removed = MemoryStack.Remove(p.name);
        return SuccessTask(removed);
    }
}
