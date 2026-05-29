using app.variable;
using app.modules.builder.code;

namespace app.modules.builder;

[Action("appSave")]
public partial class appSave : IContext
{
    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.AppSave(this);
}
