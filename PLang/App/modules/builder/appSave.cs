using App.Variables;
using App.modules.builder.code;

namespace App.modules.builder;

[System.ComponentModel.Description("Persist the built app artifact to disk after a successful build run")]
[Action("appSave")]
public partial class appSave : IContext
{
    [Code]
    public partial IBuilder Builder { get; }

    public async Task<Data.@this> Run() => await Builder.AppSave(this);
}
