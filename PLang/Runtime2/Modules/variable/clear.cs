using PLang.Runtime2.Core;

namespace PLang.Runtime2.Modules.variable;

public record clear { }

public sealed partial class ClearHandler : BaseClass<clear>
{
    protected override Task<Return> ExecuteAsync(clear? p)
    {
        MemoryStack.Clear();
        return SuccessTask();
    }
}
