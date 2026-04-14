using App.Variables;
using App.modules.assert.providers;

namespace App.modules.assert;

[Example("assert %count% greater than 0", "A=%count%, B=0")]
[Example("assert %score% greater than 50, 'Score too low'", "A=%score%, B=50, Message=Score too low")]
[Action("greaterThan")]
public partial class GreaterThan : IContext
{
    public partial Data.@this? A { get; init; }
    public partial Data.@this? B { get; init; }
    public partial Data.@this<string>? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data.@this> Run() => Task.FromResult(Assert.GreaterThan(this));
}
