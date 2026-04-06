using App.Variables;
using App.modules.builder.providers;

namespace App.modules.builder;

[Action("app.save")]
public partial class appSave : IContext
{
    [IsNotNull]
    public partial AppData App { get; init; }

    [Default(".build/app.pr")]
    public partial string Path { get; init; }

    [Provider]
    public partial IBuilderProvider Builder { get; }

    public async Task<Data> Run() => await Builder.AppSave(this);
}
