using app.Variables;
using app.modules.builder.code;
using Actions = app.Goals.Goal.Steps.Step.Actions.@this;

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
