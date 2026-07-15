using app.variable;
using app.module.action.assert.code;

namespace app.module.action.assert;

[Action("notEquals")]
public partial class NotEquals : IContext
{
    public partial data.@this? Expected { get; init; }
    public partial data.@this? Actual { get; init; }
    public partial data.@this<global::app.type.item.text.@this>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public async Task<data.@this<global::app.type.item.@bool.@this>> Run() =>
        AssertSnapshot.WithVariables(await Assert.NotEquals(this), Context);
}
