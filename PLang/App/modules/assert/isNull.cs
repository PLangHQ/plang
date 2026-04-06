using App.Variables;
using App.modules.assert.providers;

namespace App.modules.assert;

[Example("assert %result% is null", "Value=%result%")]
[Example("assert %deletedUser% is null, 'User should be deleted'", "Value=%deletedUser%, Message=User should be deleted")]
[Action("isNull")]
public partial class IsNull : IContext
{
    public partial Data.@this? Value { get; init; }
    public partial string? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data.@this> Run() => Task.FromResult(Assert.IsNull(this));
}
