using app.variable;
using app.module.assert.code;

namespace app.module.assert;

[Action("lessThan")]
public partial class LessThan : IContext
{
    public partial data.@this? A { get; init; }
    public partial data.@this? B { get; init; }
    public partial data.@this<string>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public Task<data.@this<bool>> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.LessThan(this), Context));
}
