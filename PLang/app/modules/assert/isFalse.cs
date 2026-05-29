using app.variable;
using app.modules.assert.code;

namespace app.modules.assert;

[Action("isFalse")]
public partial class IsFalse : IContext
{
    public partial data.@this? Value { get; init; }
    public partial data.@this<string>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public async Task<data.@this<bool>> Run() =>
        AssertSnapshot.WithVariables(await Assert.IsFalse(this), Context);
}
