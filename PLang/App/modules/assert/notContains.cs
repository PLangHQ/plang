using App.Variables;
using App.modules.assert.providers;

namespace App.modules.assert;

[Example("assert %text% does not contain 'hello'", "Value=%text%, Container=hello")]
[Example("assert %list% does not contain 42", "Value=%list%, Container=42")]
[Action("notContains")]
public partial class NotContains : IContext
{
    public partial Data.@this? Value { get; init; }
    public partial Data.@this? Container { get; init; }
    public partial Data.@this<string>? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data.@this> Run() =>
        Task.FromResult(AssertSnapshot.WithVariables(Assert.NotContains(this), Context));
}
