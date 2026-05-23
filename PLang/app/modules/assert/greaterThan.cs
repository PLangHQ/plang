using app.variables;
using app.modules.assert.code;

namespace app.modules.assert;

[System.ComponentModel.Description("Assert that A is greater than B; fails with an error if not")]
[Action("greaterThan")]
public partial class GreaterThan : IContext
{
    public partial data.@this? A { get; init; }
    public partial data.@this? B { get; init; }
    public partial data.@this<string>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public Task<data.@this<bool>> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.GreaterThan(this), Context));
}
