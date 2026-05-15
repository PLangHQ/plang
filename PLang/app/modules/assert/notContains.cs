using app.Variables;
using app.modules.assert.code;

namespace app.modules.assert;

[System.ComponentModel.Description("Assert that Value does not contain Container; fails with an error if it does")]
[Action("notContains")]
public partial class NotContains : IContext
{
    public partial Data.@this? Value { get; init; }
    public partial Data.@this? Container { get; init; }
    public partial Data.@this<string>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public Task<Data.@this> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.NotContains(this), Context));
}
