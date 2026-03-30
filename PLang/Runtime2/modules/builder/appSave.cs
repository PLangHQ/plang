using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Utility;
using PLang.Runtime2.modules.builder.providers;

namespace PLang.Runtime2.modules.builder;

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
