using app.variables;
using app.modules.builder.code;

namespace app.modules.builder;

[Action("goals")]
public partial class goals : IContext
{
    public partial data.@this<string> Path { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.Goals(this);
}
