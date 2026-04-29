using App.Variables;
using App.modules.assert.providers;

namespace App.modules.assert;

[System.ComponentModel.Description("Assert that Expected equals Actual; fails with an error if they differ")]
[Action("equals")]
public partial class Equals : IContext
{
    public partial Data.@this? Expected { get; init; }
    public partial Data.@this? Actual { get; init; }
    public partial Data.@this<string>? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data.@this> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.Equals(this), Context));
}
