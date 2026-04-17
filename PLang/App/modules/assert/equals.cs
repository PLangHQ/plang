using App.Variables;
using App.modules.assert.providers;

namespace App.modules.assert;

[Example("assert %result% equals 42", "Expected=42, Actual=%result%")]
[Example("assert %name% equals 'Alice', 'Name mismatch'", "Expected=Alice, Actual=%name%, Message=Name mismatch")]
[Action("equals")]
public partial class Equals : IContext
{
    public partial Data.@this? Expected { get; init; }
    public partial Data.@this? Actual { get; init; }
    public partial Data.@this<string>? Message { get; init; }

    [Provider]
    public partial IAssertProvider Assert { get; }

    public Task<Data.@this> Run() => Task.FromResult(Assert.Equals(this));
}
