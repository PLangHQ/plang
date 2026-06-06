using app.variable;
using app.module.assert.code;

namespace app.module.assert;

[Action("notEquals")]
public partial class NotEquals : IContext
{
    public partial data.@this? Expected { get; init; }
    public partial data.@this? Actual { get; init; }
    public partial data.@this<string>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public Task<data.@this<global::app.type.@bool.@this>> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.NotEquals(this), Context));
}
