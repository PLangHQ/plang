using App.Variables;
using App.modules.builder.providers;

namespace App.modules.builder;

[System.ComponentModel.Description("Persist the built app artifact to disk after a successful build run")]
[Action("appSave")]
public partial class appSave : IContext
{
    [Provider]
    public partial IBuilderProvider Builder { get; }

    public async Task<Data.@this> Run() => await Builder.AppSave(this);
}
