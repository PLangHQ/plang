using App.Variables;
using App.modules.assert.providers;

namespace App.modules.assert;

[Example("assert %isActive% is true", "Value=%isActive%")]
[Example("assert %count% is true, 'Should be truthy'", "Value=%count%, Message=Should be truthy")]
[Action("isTrue")]
public partial class IsTrue : IContext
{
    public partial Data.@this? Value { get; init; }
    public partial string? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data.@this> Run() => Task.FromResult(Assert.IsTrue(this));
}
