using app.Variables;
using app.modules.assert.code;

namespace app.modules.assert;

[System.ComponentModel.Description("Assert that Expected does not equal Actual; fails with an error if they match")]
[Action("notEquals")]
public partial class NotEquals : IContext
{
    public partial Data.@this? Expected { get; init; }
    public partial Data.@this? Actual { get; init; }
    public partial Data.@this<string>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public Task<Data.@this> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.NotEquals(this), Context));
}
