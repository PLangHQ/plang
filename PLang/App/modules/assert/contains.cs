using App.Variables;
using App.modules.assert.code;

namespace App.modules.assert;

[ModuleDescription("Test assertions that fail the step with a descriptive error when the condition is not met")]
[System.ComponentModel.Description("Assert that Value contains Container; fails with an error if not")]
[Action("contains")]
public partial class Contains : IContext
{
    public partial Data.@this? Value { get; init; }
    public partial Data.@this? Container { get; init; }
    public partial Data.@this<string>? Message { get; init; }

    [Provider]
    public partial IAssert Assert { get; }

    public Task<Data.@this> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.Contains(this), Context));
}
