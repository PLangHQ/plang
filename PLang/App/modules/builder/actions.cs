using App.Engine.Variables;
using App.modules.builder.providers;

namespace App.modules.builder;

[Action("actions")]
public partial class GetActions : IContext
{
    [Provider]
    public partial IBuilderProvider Builder { get; }

    public async Task<Data> Run() => await Builder.Actions(this);
}
