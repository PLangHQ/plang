using App.Variables;
using App.modules.builder.providers;
using Actions = App.Goals.Goal.Steps.Step.Actions.@this;

namespace App.modules.builder;

[System.ComponentModel.Description("Validate an action set against known modules and parameter schemas")]
[Action("validate")]
public partial class validate : IContext
{
    public partial Data.@this<Actions>? Actions { get; init; }

    [Provider]
    public partial IBuilderProvider Builder { get; }

    public async Task<Data.@this> Run() => await Builder.Validate(this);
}
