using app.module.assert.code;

namespace app.module.assert;

[Action("isTrue")]
public partial class IsTrue : IContext
{
    public partial data.@this? Value { get; init; }
    public partial data.@this<global::app.type.item.text.@this>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public async Task<data.@this<global::app.type.item.@bool.@this>> Start() =>
        await Assert.IsTrue(this);
}
