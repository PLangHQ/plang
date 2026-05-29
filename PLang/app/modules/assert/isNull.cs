using app.variable;
using app.modules.assert.code;

namespace app.modules.assert;

[Action("isNull")]
public partial class IsNull : IContext
{
    public partial data.@this? Value { get; init; }
    public partial data.@this<string>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public Task<data.@this<bool>> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.IsNull(this), Context));
}
