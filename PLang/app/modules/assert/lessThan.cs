using app.variables;
using app.modules.assert.code;

namespace app.modules.assert;

[System.ComponentModel.Description("Assert that A is less than B; fails with an error if not")]
[Action("lessThan")]
public partial class LessThan : IContext
{
    public partial data.@this? A { get; init; }
    public partial data.@this? B { get; init; }
    public partial data.@this<string>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public Task<data.@this> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.LessThan(this), Context));
}
