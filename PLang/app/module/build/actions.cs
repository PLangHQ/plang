using app.variable;
using app.module.build.code;

namespace app.module.build;

[Action("actions")]
public partial class GetActions : IContext
{
    /// <summary>
    /// Optional filter — the <c>module.action</c> names to restrict the catalog
    /// to. Null or empty returns the full catalog. The builder's Compile step
    /// passes the planner's action set here so the prompt carries only the
    /// relevant rows.
    /// </summary>
    public partial data.@this<global::app.type.list.@this>? Actions { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this<global::app.type.clr.@this<global::app.goal.steps.step.actions.@this>>> Run()
    {
        var result = await Builder.Actions(this);
        return data.@this<global::app.type.clr.@this<global::app.goal.steps.step.actions.@this>>.From(result);
    }
}
