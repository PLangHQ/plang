using App.Variables;
using App.modules.builder.code;

namespace App.modules.builder;

[System.ComponentModel.Description("Return the list of registered PLang type names available to the builder")]
[Action("types")]
public partial class types : IContext
{
    [Provider]
    public partial IBuilder Builder { get; }

    public Task<Data.@this> Run() => Task.FromResult(Builder.Types(this));
}
