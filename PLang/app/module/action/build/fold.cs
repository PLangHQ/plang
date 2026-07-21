using app.variable;
using app.module.action.build.code;
using Goal = app.goal.@this;

namespace app.module.action.build;

/// <summary>
/// Re-parents a goal's indented sub-steps into the preceding condition's gate action
/// <c>Child</c> — the deterministic, build-time half of the tree (the LLM emits inline
/// bodies; this folds indent-authored sub-steps). Runs post-compile, pre-save: the flat
/// steps already carry their compiled actions, so the gate action exists to attach to.
/// MOVES real steps (own Text, LineNumber, compile) — never synthesizes.
/// </summary>
[Action("fold")]
public partial class fold : IContext
{
    [IsNotNull]
    public partial data.@this<Goal> Goal { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.Fold(this);
}
