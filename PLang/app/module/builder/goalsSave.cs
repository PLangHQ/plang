using app.variable;
using app.module.builder.code;
using Goal = app.goal.@this;

namespace app.module.builder;

[Action("goalsSave")]
public partial class goalsSave : IContext
{
    [IsNotNull]
    public partial data.@this<Goal> Goal { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.GoalsSave(this);
}
