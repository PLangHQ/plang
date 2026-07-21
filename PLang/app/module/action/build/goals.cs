using app.variable;
using app.module.action.build.code;
using Goal = app.goal.@this;

namespace app.module.action.build;

[Action("goals")]
public partial class goals : IContext
{
    public partial data.@this<global::app.type.item.path.@this> Path { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this<global::app.type.item.list.@this<Goal>>> Run()
    {
        // Builder.Goals hands back a plang list<goal> (warnings ride the Data) — forward it, no peel.
        var result = await Builder.Goals(this);
        return data.@this<global::app.type.item.list.@this<Goal>>.From(result);
    }
}
