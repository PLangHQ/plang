using app.module.action.assert.code;

namespace app.module.action.assert;

[Action("notContains")]
public partial class NotContains : IContext
{
    public partial data.@this? Value { get; init; }
    public partial data.@this? Container { get; init; }
    public partial data.@this<global::app.type.item.text.@this>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public async Task<data.@this<global::app.type.item.@bool.@this>> Start() =>
        await Assert.NotContains(this);
}
