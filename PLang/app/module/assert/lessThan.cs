using app.variable;
using app.module.assert.code;

namespace app.module.assert;

[Action("lessThan")]
public partial class LessThan : IContext
{
    public partial data.@this? A { get; init; }
    public partial data.@this? B { get; init; }
    public partial data.@this<global::app.type.item.text.@this>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public async Task<data.@this<global::app.type.item.@bool.@this>> Run() =>
        AssertSnapshot.WithVariables(await Assert.LessThan(this), Context);
}
