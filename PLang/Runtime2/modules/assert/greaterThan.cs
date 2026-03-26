using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.assert.providers;

namespace PLang.Runtime2.modules.assert;

[Action("greaterThan")]
public partial class GreaterThan : IContext
{
    public partial Data? A { get; init; }
    public partial Data? B { get; init; }
    public partial string? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data> Run() => Task.FromResult(Assert.GreaterThan(this));
}
