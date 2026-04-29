using App.Variables;
using App.modules.builder.providers;

namespace App.modules.builder;

[System.ComponentModel.Description("Load all goal files from a directory for processing by the builder")]
[Action("goals")]
public partial class goals : IContext
{
    public partial Data.@this<string> Path { get; init; }

    [Provider]
    public partial IBuilderProvider Builder { get; }

    public async Task<Data.@this> Run() => await Builder.Goals(this);
}
