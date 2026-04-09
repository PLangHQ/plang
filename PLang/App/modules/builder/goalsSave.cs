using App.Variables;
using App.modules.builder.providers;
using Goal = App.Goals.Goal.@this;

namespace App.modules.builder;

[Action("goals.save")]
public partial class goalsSave : IContext
{
    [IsNotNull]
    public partial Goal Goal { get; init; }

    [Provider]
    public partial IBuilderProvider Builder { get; }

    public async Task<Data.@this> Run() => await Builder.GoalsSave(this);
}
