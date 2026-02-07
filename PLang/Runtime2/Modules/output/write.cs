using PLang.Runtime2.Core;

namespace PLang.Runtime2.Modules.output;

public record write
{
    public virtual object content { get; init; } = null!;
}

public sealed partial class WriteHandler : BaseClass<write>
{
    protected override Task<Return> ExecuteAsync(write? p)
    {
        if (p?.content != null)
        {
            Console.WriteLine(p.content);
        }

        return SuccessTask();
    }
}
