using app.variables;
using app.modules.assert.code;

namespace app.modules.assert;

[System.ComponentModel.Description("Assert that Value is falsy (false, 0, null, or empty); fails with an error if truthy")]
[Action("isFalse")]
public partial class IsFalse : IContext
{
    public partial data.@this? Value { get; init; }
    public partial data.@this<string>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public async Task<data.@this> Run() =>
        AssertSnapshot.WithVariables(await Assert.IsFalse(this), Context);
}
