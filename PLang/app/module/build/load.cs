using app.variable;
using app.module.build.code;

namespace app.module.build;

[Action("load")]
public partial class load : IContext
{
    [Default(".")]
    public partial data.@this<global::app.type.item.path.@this> Path { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.Load(this);
}
