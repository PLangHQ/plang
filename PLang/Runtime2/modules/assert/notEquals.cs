using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.assert.providers;

namespace PLang.Runtime2.modules.assert;

[Example("assert %result% not equals 0", "Expected=0, Actual=%result%")]
[Example("assert %status% not equals 'error'", "Expected=error, Actual=%status%")]
[Action("notEquals")]
public partial class NotEquals : IContext
{
    public partial Data? Expected { get; init; }
    public partial Data? Actual { get; init; }
    public partial string? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data> Run() => Task.FromResult(Assert.NotEquals(this));
}
