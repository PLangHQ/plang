using app.Variables;
using app.modules.assert.code;

namespace app.modules.assert;

[System.ComponentModel.Description("Assert that A is greater than B; fails with an error if not")]
[Action("greaterThan")]
public partial class GreaterThan : IContext
{
    public partial Data.@this? A { get; init; }
    public partial Data.@this? B { get; init; }
    public partial Data.@this<string>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public Task<Data.@this> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.GreaterThan(this), Context));
}
