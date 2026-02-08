using PLang.Runtime2.Memory;

namespace PLang.Runtime2.actions.variable;

public sealed partial class ClearHandler : BaseClass<NullParams>
{
    protected override Task<Data> ExecuteAsync(NullParams p)
    {
        MemoryStack.Clear();
        return SuccessTask(new types.variable { });
    }
}
