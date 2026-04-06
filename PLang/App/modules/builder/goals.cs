using App.Engine.Variables;
using App.modules.builder.providers;

namespace App.modules.builder;

[Action("goals")]
public partial class goals : IContext
{
    public partial string Path { get; init; }

    [Provider]
    public partial IBuilderProvider Builder { get; }

    public async Task<Data> Run() => await Builder.Goals(this);
}
