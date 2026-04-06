using App.Engine.Variables;
using App.modules.builder.providers;
using Goal = App.Engine.Goals.Goal.@this;

namespace App.modules.builder;

[Action("goals.save")]
public partial class goalsSave : IContext
{
    [IsNotNull]
    public partial List<Goal> Goals { get; init; }

    [Provider]
    public partial IBuilderProvider Builder { get; }

    public async Task<Data> Run() => await Builder.GoalsSave(this);
}
