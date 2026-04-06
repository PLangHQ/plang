using App.Engine.Variables;
using App.modules.assert.providers;

namespace App.modules.assert;

[Example("assert %user% is not null", "Value=%user%")]
[Example("assert %response% is not null, 'API should return data'", "Value=%response%, Message=API should return data")]
[Action("isNotNull")]
public partial class IsNotNull : IContext
{
    public partial Data? Value { get; init; }
    public partial string? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data> Run() => Task.FromResult(Assert.IsNotNull(this));
}
