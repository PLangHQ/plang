using app.variable;
using app.module.action.build.code;
using Goal = app.goal.@this;

namespace app.module.action.build;

/// <summary>
/// Hands a decider answer to each step of the goal (<c>step.Pick</c>): each step takes the answers
/// under its own question ids, whichever stage asked them, and works out its picks again. Stage 1's
/// answer, then stage 2's, go through the same action.
/// </summary>
[Action("pick")]
public partial class pick : IContext
{
    [IsNotNull]
    public partial data.@this<Goal> Goal { get; init; }

    /// <summary>The decider's answer: <c>{id: {choice, confidence, probabilities, noul}}</c>.</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.dict.@this> Answer { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.Pick(this);
}
