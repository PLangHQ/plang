using App.Variables;
using App.modules.assert.code;

namespace App.modules.assert;

[System.ComponentModel.Description("Assert that A is less than B; fails with an error if not")]
[Action("lessThan")]
public partial class LessThan : IContext
{
    public partial Data.@this? A { get; init; }
    public partial Data.@this? B { get; init; }
    public partial Data.@this<string>? Message { get; init; }

    [Provider]
    public partial IAssert Assert { get; }

    public Task<Data.@this> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.LessThan(this), Context));
}
