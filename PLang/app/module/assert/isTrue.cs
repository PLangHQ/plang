using app.variable;
using app.module.assert.code;

namespace app.module.assert;

[Action("isTrue")]
public partial class IsTrue : IContext
{
    public partial data.@this? Value { get; init; }
    public partial data.@this<string>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public async Task<data.@this<global::app.type.@bool.@this>> Run() =>
        AssertSnapshot.WithVariables(await Assert.IsTrue(this), Context);
}
