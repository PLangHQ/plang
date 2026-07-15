using app.variable;
using app.module.action.assert.code;

namespace app.module.action.assert;

[Action("isNull")]
public partial class IsNull : IContext
{
    public partial data.@this? Value { get; init; }
    public partial data.@this<global::app.type.item.text.@this>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public Task<data.@this<global::app.type.item.@bool.@this>> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.IsNull(this), Context));
}
