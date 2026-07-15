using app.variable;
using app.module.action.build.code;

namespace app.module.action.build;

[Action("appSave")]
public partial class appSave : IContext
{
    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.AppSave(this);
}
