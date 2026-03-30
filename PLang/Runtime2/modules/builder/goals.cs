using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.builder.providers;

namespace PLang.Runtime2.modules.builder;

[Action("goals")]
public partial class goals : IContext
{
    public partial string Path { get; init; }

    [Provider]
    public partial IBuilderProvider Builder { get; }

    public async Task<Data> Run() => await Builder.GetGoals(this);
}
