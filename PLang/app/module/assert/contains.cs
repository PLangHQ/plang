using app.variable;
using app.module.assert.code;

namespace app.module.assert;

[Action("contains")]
public partial class Contains : IContext
{
    public partial data.@this? Value { get; init; }
    public partial data.@this? Container { get; init; }
    public partial data.@this<string>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public Task<data.@this<bool>> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.Contains(this), Context));
}
