using app.variables;
using app.modules.assert.code;

namespace app.modules.assert;

[Action("notContains")]
public partial class NotContains : IContext
{
    public partial data.@this? Value { get; init; }
    public partial data.@this? Container { get; init; }
    public partial data.@this<string>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public Task<data.@this<bool>> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.NotContains(this), Context));
}
