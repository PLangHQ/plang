using App.Variables;
using App.modules.builder.code;

namespace App.modules.builder;

[System.ComponentModel.Description("Load all goal files from a directory for processing by the builder")]
[Action("goals")]
public partial class goals : IContext
{
    public partial Data.@this<string> Path { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<Data.@this> Run() => await Builder.Goals(this);
}
