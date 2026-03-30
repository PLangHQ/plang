using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.builder.providers;

namespace PLang.Runtime2.modules.builder;

[Action("app")]
public partial class app : IContext
{
    [Default(".")]
    public partial string Path { get; init; }

    [Provider]
    public partial IBuilderProvider Builder { get; }

    public async Task<Data> Run() => await Builder.App(this);
}
