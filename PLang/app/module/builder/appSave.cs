using app.variable;
using app.module.builder.code;

namespace app.module.builder;

[Action("appSave")]
public partial class appSave : IContext
{
    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.AppSave(this);
}
