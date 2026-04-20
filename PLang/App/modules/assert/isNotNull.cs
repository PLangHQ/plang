using App.Variables;
using App.modules.assert.providers;

namespace App.modules.assert;

[Example("assert %user% is not null", "Value=%user%")]
[Example("assert %response% is not null, 'API should return data'", "Value=%response%, Message=API should return data")]
[Action("isNotNull")]
public partial class IsNotNull : IContext
{
    public partial Data.@this? Value { get; init; }
    public partial Data.@this<string>? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data.@this> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.IsNotNull(this), Context));
}
