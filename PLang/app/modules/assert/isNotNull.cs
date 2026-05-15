using app.Variables;
using app.modules.assert.code;

namespace app.modules.assert;

[System.ComponentModel.Description("Assert that Value is not null or empty; fails with an error if null")]
[Action("isNotNull")]
public partial class IsNotNull : IContext
{
    public partial Data.@this? Value { get; init; }
    public partial Data.@this<string>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public Task<Data.@this> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.IsNotNull(this), Context));
}
