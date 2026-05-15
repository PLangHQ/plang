using app.variables;
using app.modules.builder.code;
using Actions = app.goals.goal.steps.step.actions.@this;

namespace app.modules.builder;

[System.ComponentModel.Description("Validate an action set against known modules and parameter schemas")]
[Action("validate")]
public partial class validate : IContext
{
    public partial data.@this<Actions>? Actions { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.Validate(this);
}
