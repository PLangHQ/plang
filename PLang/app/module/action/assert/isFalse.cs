using app.variable;
using app.module.action.assert.code;

namespace app.module.action.assert;

[Action("isFalse")]
public partial class IsFalse : IContext
{
    public partial data.@this? Value { get; init; }
    public partial data.@this<global::app.type.item.text.@this>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public async Task<data.@this<global::app.type.item.@bool.@this>> Run() =>
        AssertSnapshot.WithVariables(await Assert.IsFalse(this), Context);
}
