using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.builder.providers;
using Actions = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this;

namespace PLang.Runtime2.modules.builder;

[Action("actions.validate")]
public partial class validate : IContext
{
    public partial Actions? Actions { get; init; }

    [Provider]
    public partial IBuilderProvider Builder { get; }

    public async Task<Data> Run() => await Builder.Validate(this);
}
