using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.assert.providers;

namespace PLang.Runtime2.modules.assert;

/// PLang: - assert %result% is null
/// PLang: - assert %deletedUser% is null, "User should be deleted"
[Action("isNull")]
public partial class IsNull : IContext
{
    public partial Data? Value { get; init; }
    public partial string? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data> Run() => Task.FromResult(Assert.IsNull(this));
}
