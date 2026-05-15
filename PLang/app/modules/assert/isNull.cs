using app.Variables;
using app.modules.assert.code;

namespace app.modules.assert;

[System.ComponentModel.Description("Assert that Value is null or unset; fails with an error if it has a value")]
[Action("isNull")]
public partial class IsNull : IContext
{
    public partial data.@this? Value { get; init; }
    public partial data.@this<string>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public Task<data.@this> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.IsNull(this), Context));
}
