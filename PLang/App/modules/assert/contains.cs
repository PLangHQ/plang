using App.Variables;
using App.modules.assert.providers;

namespace App.modules.assert;

[Example("assert %text% contains 'hello'", "Value=%text%, Container=hello")]
[Example("assert %list% contains 42", "Value=%list%, Container=42")]
[Action("contains")]
public partial class Contains : IContext
{
    public partial Data.@this? Value { get; init; }
    public partial Data.@this? Container { get; init; }
    public partial Data.@this<string>? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data.@this> Run() => Task.FromResult(Assert.Contains(this));
}
