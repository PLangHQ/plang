using app.module.build.code;

namespace app.module.build;

[Action("appSave")]
public partial class appSave : IContext
{
    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Start() => await Builder.AppSave(this);
}
