using app.variables;
using app.modules.builder.code;
using Goal = app.goals.goal.@this;

namespace app.modules.builder;

[Action("goalsSave")]
public partial class goalsSave : IContext
{
    [IsNotNull]
    public partial data.@this<Goal> Goal { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.GoalsSave(this);
}
