using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.assert.providers;

namespace PLang.Runtime2.modules.assert;

[Example("assert %result% equals 42", "Expected=42, Actual=%result%")]
[Example("assert %name% equals 'Alice', 'Name mismatch'", "Expected=Alice, Actual=%name%, Message=Name mismatch")]
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
