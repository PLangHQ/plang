using app.variable;
using app.modules.assert.code;

namespace app.modules.assert;

[Action("equals")]
public partial class Equals : IContext
{
    public partial data.@this? Expected { get; init; }
    public partial data.@this? Actual { get; init; }
    public partial data.@this<string>? Message { get; init; }

    [Code]
    public partial IAssert Assert { get; }

    public Task<data.@this<bool>> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.Equals(this), Context));
}
