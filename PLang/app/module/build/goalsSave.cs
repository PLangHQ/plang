using app.variable;
using app.module.build.code;
using Goal = app.goal.@this;

namespace app.module.build;

[Action("goalsSave")]
public partial class goalsSave : IContext
{
    [IsNotNull]
    public partial data.@this<Goal> Goal { get; init; }

    /// <summary>The TARGET app being built (the self-hosted builder's own app is a different
    /// one) — carried strictly as a typed host, no reflection guess. Optional: an isolated
    /// test may save a hand-built goal without an app anchor.</summary>
    public partial data.@this<global::app.type.clr.@this<global::app.@this>>? App { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.GoalsSave(this);
}
