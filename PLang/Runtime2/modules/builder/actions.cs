using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.builder.providers;

namespace PLang.Runtime2.modules.builder;

[Action("actions")]
public partial class GetActions : IContext
{
    [Provider]
    public partial IBuilderProvider Builder { get; }

    public async Task<Data> Run() => await Builder.GetActions(this);
}
