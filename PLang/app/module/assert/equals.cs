using app.module.assert.code;

namespace app.module.assert;

[Action("equals")]
public partial class Equals : IContext
{
    public partial data.@this? Expected { get; init; }
    public partial data.@this? Actual { get; init; }
    public partial data.@this<global::app.type.item.text.@this>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public async Task<data.@this<global::app.type.item.@bool.@this>> Start() =>
        await Assert.Equals(this);
}
