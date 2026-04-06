using App.Variables;
using App.modules.builder.providers;

namespace App.modules.builder;

[Action("types")]
public partial class types : IContext
{
    [Provider]
    public partial IBuilderProvider Builder { get; }

    public Task<Data> Run() => Task.FromResult(Builder.Types(this));
}
