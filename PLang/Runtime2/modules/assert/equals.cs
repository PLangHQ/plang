using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.assert.providers;

namespace PLang.Runtime2.modules.assert;

[Action("equals")]
public partial class Equals : IContext
{
    public partial Data? Expected { get; init; }
    public partial Data? Actual { get; init; }
    public partial string? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data> Run() => Task.FromResult(Assert.Equals(this));
}
