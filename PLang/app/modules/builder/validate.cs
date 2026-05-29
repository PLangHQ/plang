using app.variable;
using app.modules.builder.code;
using Actions = app.goal.steps.step.actions.@this;

namespace app.modules.builder;

[Action("validate")]
public partial class validate : IContext
{
    public partial data.@this<Actions>? Actions { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.Validate(this);
}
