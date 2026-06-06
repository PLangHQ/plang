using app.variable;
using app.module.assert.code;

namespace app.module.assert;

[Action("isNotNull")]
public partial class IsNotNull : IContext
{
    public partial data.@this? Value { get; init; }
    public partial data.@this<global::app.type.text.@this>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public Task<data.@this<global::app.type.@bool.@this>> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.IsNotNull(this), Context));
}
