using app.variable;
using app.module.action.build.code;
using Goal = app.goal.@this;

namespace app.module.action.build;

/// <summary>
/// Refuses a stage-3 answer that does not line up with the goal's steps — one entry per step, in
/// order, each labelled with its own index and holding actions — before any step takes its actions.
/// A step grafts its entry by index, so a dropped, merged or renumbered entry would otherwise hand one
/// step another step's actions and the .pr would no longer read like its .goal.
/// </summary>
[Action("match")]
public partial class match : IContext
{
    [IsNotNull]
    public partial data.@this<Goal> Goal { get; init; }

    /// <summary>The stage-3 answer: <c>{step: [{index, action: [...]}]}</c>.</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.dict.@this> Answer { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.Match(this);
}
