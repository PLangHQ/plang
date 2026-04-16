using App.Variables;
using App.modules.assert.providers;

namespace App.modules.assert;

[Example("assert %elapsed% less than 1000", "A=%elapsed%, B=1000")]
[Example("assert %retries% less than 5, 'Too many retries'", "A=%retries%, B=5, Message=Too many retries")]
[Action("lessThan")]
public partial class LessThan : IContext
{
    public partial Data.@this? A { get; init; }
    public partial Data.@this? B { get; init; }
    public partial Data.@this<string>? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data.@this> Run() => Task.FromResult(Assert.LessThan(this));
}
