using app.variable;
using app.module.assert.code;

namespace app.module.assert;

[Action("equals")]
public partial class Equals : IContext
{
    public partial data.@this? Expected { get; init; }
    public partial data.@this? Actual { get; init; }
    public partial data.@this<global::app.type.text.@this>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public async Task<data.@this<global::app.type.@bool.@this>> Run() =>
        AssertSnapshot.WithVariables(await Assert.Equals(this), Context);
}
