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

    public Task<data.@this<global::app.type.@bool.@this>> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.Equals(this), Context));
}
