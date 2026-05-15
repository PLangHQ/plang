using app.Variables;
using app.modules.builder.code;

namespace app.modules.builder;

[System.ComponentModel.Description("Return the list of registered PLang type names available to the builder")]
[Action("types")]
public partial class types : IContext
{
    [Code]
    public partial IBuilder Builder { get; }

    public Task<data.@this> Run() => Task.FromResult(Builder.Types(this));
}
