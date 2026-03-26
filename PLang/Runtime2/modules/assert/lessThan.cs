using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.assert.providers;

namespace PLang.Runtime2.modules.assert;

[Example("assert %elapsed% less than 1000", "A=%elapsed%, B=1000")]
[Example("assert %retries% less than 5, 'Too many retries'", "A=%retries%, B=5, Message=Too many retries")]
[Action("lessThan")]
public partial class LessThan : IContext
{
    public partial Data? A { get; init; }
    public partial Data? B { get; init; }
    public partial string? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data> Run() => Task.FromResult(Assert.LessThan(this));
}
