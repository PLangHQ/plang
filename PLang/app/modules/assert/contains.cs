using app.variables;
using app.modules.assert.code;

namespace app.modules.assert;

[ModuleDescription("Test assertions that fail the step with a descriptive error when the condition is not met")]
[System.ComponentModel.Description("Assert that Value contains Container; fails with an error if not")]
[Action("contains")]
public partial class Contains : IContext
{
    public partial data.@this? Value { get; init; }
    public partial data.@this? Container { get; init; }
    public partial data.@this<string>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public Task<data.@this<bool>> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.Contains(this), Context));
}
