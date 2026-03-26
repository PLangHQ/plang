using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.assert.providers;

namespace PLang.Runtime2.modules.assert;

/// PLang: - assert %result% not equals 0
/// PLang: - assert %status% not equals "error"
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
