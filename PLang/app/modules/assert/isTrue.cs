using app.variables;
using app.modules.assert.code;

namespace app.modules.assert;

[System.ComponentModel.Description("Assert that Value is truthy (non-null, non-zero, non-empty); fails with an error if falsy")]
[Action("isTrue")]
public partial class IsTrue : IContext
{
    public partial data.@this? Value { get; init; }
    public partial data.@this<string>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public async Task<data.@this<bool>> Run() =>
        AssertSnapshot.WithVariables(await Assert.IsTrue(this), Context);
}
