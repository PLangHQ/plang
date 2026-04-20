using App.Variables;
using App.modules.assert.providers;

namespace App.modules.assert;

[Example("assert %isDeleted% is false", "Value=%isDeleted%")]
[Example("assert %error% is false", "Value=%error%")]
[Action("isFalse")]
public partial class IsFalse : IContext
{
    public partial Data.@this? Value { get; init; }
    public partial Data.@this<string>? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data.@this> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.IsFalse(this), Context));
}
