using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;
using System.Collections;

namespace PLang.Runtime2.modules.assert;

[Action("contains")]
public partial class Contains : IContext
{
    public partial object? Value { get; init; }
    public partial object? Container { get; init; }
    public partial string? Message { get; init; }

    public Task<Data> Run()
    {
        if (AssertHelper.Contains(Value, Container))
            return Task.FromResult(Data.Ok(true));

        return Task.FromResult(Data.FromError(
            new AssertionError(
                AssertHelper.FormatValue(Container),
                Value,
                Message ?? "Container does not contain value")));
    }
}
