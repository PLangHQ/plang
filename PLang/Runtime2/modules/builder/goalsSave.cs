using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.builder.providers;
using Goal = PLang.Runtime2.Engine.Goals.Goal.@this;

namespace PLang.Runtime2.modules.builder;

[Action("goals.save")]
public partial class goalsSave : IContext
{
    [IsNotNull]
    public partial List<Goal> Goals { get; init; }

    [Provider]
    public partial IBuilderProvider Builder { get; }

    public async Task<Data> Run() => await Builder.SaveGoals(this);
}
