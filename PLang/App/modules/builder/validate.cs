using App.Engine.Variables;
using App.modules.builder.providers;
using Actions = App.Engine.Goals.Goal.Steps.Step.Actions.@this;

namespace App.modules.builder;

[Action("actions.validate")]
public partial class validate : IContext
{
    public partial Actions? Actions { get; init; }

    [Provider]
    public partial IBuilderProvider Builder { get; }

    public async Task<Data> Run() => await Builder.Validate(this);
}
