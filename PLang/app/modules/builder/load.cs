using app.variable;
using app.modules.builder.code;

namespace app.modules.builder;

[Action("load")]
public partial class load : IContext
{
    [Default(".")]
    public partial data.@this<global::app.types.path.@this> Path { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.Load(this);
}
