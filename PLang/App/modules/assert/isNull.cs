using App.Variables;
using App.modules.assert.code;

namespace App.modules.assert;

[System.ComponentModel.Description("Assert that Value is null or unset; fails with an error if it has a value")]
[Action("isNull")]
public partial class IsNull : IContext
{
    public partial Data.@this? Value { get; init; }
    public partial Data.@this<string>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public Task<Data.@this> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.IsNull(this), Context));
}
