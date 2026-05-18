using app.variables;
using app.modules.builder.code;

namespace app.modules.builder;

[System.ComponentModel.Description("Persist the built app artifact to disk after a successful build run")]
[Action("appSave")]
public partial class appSave : IContext
{
    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.AppSave(this);
}
