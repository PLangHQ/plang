using app.variable;
using app.module.action.build.code;
using Goal = app.goal.@this;

namespace app.module.action.build;

/// <summary>
/// Reads the stage-3 answer into the goal's steps: one line per step in formal, each read, checked and
/// taken as that step's code (goal.step.list.Read). The steps it refuses stay open, and the error names
/// them and every problem at once; a retry answers only those steps, and the others keep their code.
/// </summary>
[Action("match")]
public partial class match : IContext
{
    [IsNotNull]
    public partial data.@this<Goal> Goal { get; init; }

    /// <summary>The stage-3 answer: one line per step, <c>[i] module.action(Name=value); …</c>.</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.text.@this> Answer { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.Match(this);
}
