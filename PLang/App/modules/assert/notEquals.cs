using App.Variables;
using App.modules.assert.providers;

namespace App.modules.assert;

[Example("assert %result% not equals 0", "Expected=0, Actual=%result%")]
[Example("assert %status% not equals 'error'", "Expected=error, Actual=%status%")]
[Action("notEquals")]
public partial class NotEquals : IContext
{
    public partial Data.@this? Expected { get; init; }
    public partial Data.@this? Actual { get; init; }
    public partial Data.@this<string>? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data.@this> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.NotEquals(this), Context));
}
