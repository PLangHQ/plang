using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.assert.providers;

namespace PLang.Runtime2.modules.assert;

[Example("assert %text% contains 'hello'", "Value=%text%, Container=hello")]
[Example("assert %list% contains 42", "Value=%list%, Container=42")]
[Action("contains")]
public partial class Contains : IContext
{
    public partial Data? Value { get; init; }
    public partial Data? Container { get; init; }
    public partial string? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data> Run() => Task.FromResult(Assert.Contains(this));
}
