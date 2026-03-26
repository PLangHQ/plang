using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.assert.providers;

namespace PLang.Runtime2.modules.assert;

/// PLang: - assert %isActive% is true
/// PLang: - assert %count% is true, "Should be truthy"
[Action("isTrue")]
public partial class IsTrue : IContext
{
    public partial Data? Value { get; init; }
    public partial string? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data> Run() => Task.FromResult(Assert.IsTrue(this));
}
