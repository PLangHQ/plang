using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.assert;

[Action("lessThan")]
public partial class LessThan : IContext
{
    public partial object A { get; init; }
    public partial object B { get; init; }
    public partial string? Message { get; init; }

    public Task<Data> Run()
    {
        var comparison = AssertHelper.Compare(A, B);
        if (comparison < 0)
            return Task.FromResult(Data.Ok(true));

        return Task.FromResult(Data.FromError(
            new AssertionError($"< {AssertHelper.FormatValue(B)}", A,
                Message ?? $"Expected {AssertHelper.FormatValue(A)} < {AssertHelper.FormatValue(B)}")));
    }
}
