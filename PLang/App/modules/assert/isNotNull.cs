using App.Variables;
using App.modules.assert.code;

namespace App.modules.assert;

[System.ComponentModel.Description("Assert that Value is not null or empty; fails with an error if null")]
[Action("isNotNull")]
public partial class IsNotNull : IContext
{
    public partial Data.@this? Value { get; init; }
    public partial Data.@this<string>? Message { get; init; }

    [Provider]
    public partial IAssert Assert { get; }

    public Task<Data.@this> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.IsNotNull(this), Context));
}
