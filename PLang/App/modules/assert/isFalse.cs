using App.Engine.Variables;
using App.modules.assert.providers;

namespace App.modules.assert;

[Example("assert %isDeleted% is false", "Value=%isDeleted%")]
[Example("assert %error% is false", "Value=%error%")]
[Action("isFalse")]
public partial class IsFalse : IContext
{
    public partial Data? Value { get; init; }
    public partial string? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data> Run() => Task.FromResult(Assert.IsFalse(this));
}
