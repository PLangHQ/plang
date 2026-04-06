using App.Variables;
using App.modules.builder.providers;

namespace App.modules.builder;

[Action("app")]
public partial class app : IContext
{
    [Default(".")]
    public partial string Path { get; init; }

    [Provider]
    public partial IBuilderProvider Builder { get; }

    public async Task<Data> Run() => await Builder.App(this);
}
