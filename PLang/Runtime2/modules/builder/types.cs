using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.builder.providers;

namespace PLang.Runtime2.modules.builder;

[Action("types")]
public partial class types : IContext
{
    [Provider]
    public partial IBuilderProvider Builder { get; }

    public Task<Data> Run() => Task.FromResult(Builder.Types(this));
}
