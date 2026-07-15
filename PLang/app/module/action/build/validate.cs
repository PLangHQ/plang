using app.variable;
using app.module.action.build.code;
using Actions = app.goal.steps.step.actions.@this;

namespace app.module.action.build;

[Action("validate")]
public partial class validate : IContext
{
    public partial data.@this<global::app.type.clr.@this<Actions>>? Actions { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.Validate(this);
}
