using App.Variables;
using App.modules.builder.providers;

namespace App.modules.builder;

[Action("app.save")]
public partial class appSave : IContext
{
    [Provider]
    public partial IBuilderProvider Builder { get; }

    public async Task<Data.@this> Run() => await Builder.AppSave(this);
}
