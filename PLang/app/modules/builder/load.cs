using app.Variables;
using app.modules.builder.code;

namespace app.modules.builder;

[System.ComponentModel.Description("Load the app-level build context from a directory path")]
[Action("load")]
public partial class load : IContext
{
    [Default(".")]
    public partial data.@this<string> Path { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.Load(this);
}
