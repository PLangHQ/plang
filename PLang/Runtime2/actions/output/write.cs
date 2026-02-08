using PLang.Runtime2.Memory;

namespace PLang.Runtime2.actions.output;

public record write
{
    public virtual object content { get; init; } = null!;
}

public sealed partial class WriteHandler : BaseClass<write>
{
    protected override async Task<Data> ExecuteAsync(write p)
    {
        var result = await Engine.IO.WriteTextAsync(Runtime2.IO.IO.StdOut, p.content?.ToString());
        if (!result.Success) return result;
        return Success(new types.output { content = p.content, channel = Runtime2.IO.IO.StdOut });
    }
}
