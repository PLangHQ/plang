using app.variable;
using app.module.action.build.code;

namespace app.module.action.build;

/// <summary>Finishes a step's freshly grafted actions (the construction passes) and asks the step
/// for its verdict — the same judgement goal.Validate walks at save.</summary>
[Action("validate")]
public partial class validate : IContext
{
    [IsNotNull]
    public partial data.@this<Step> Step { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.Validate(this);
}
