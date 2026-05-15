using app.Variables;
using app.modules.builder.code;
using Goal = app.Goals.Goal.@this;

namespace app.modules.builder;

[System.ComponentModel.Description("Save a built goal's .pr file to disk, completing the build for that goal")]
[Action("goalsSave")]
public partial class goalsSave : IContext
{
    [IsNotNull]
    public partial Data.@this<Goal> Goal { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<Data.@this> Run() => await Builder.GoalsSave(this);
}
