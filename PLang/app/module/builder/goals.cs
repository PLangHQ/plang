using app.variable;
using app.module.builder.code;
using Goal = app.goal.@this;

namespace app.module.builder;

[Action("goals")]
public partial class goals : IContext
{
    public partial data.@this<global::app.type.path.@this> Path { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this<global::app.type.list.@this<Goal>>> Run()
    {
        var result = await Builder.Goals(this);
        if (!result.Success) return data.@this<global::app.type.list.@this<Goal>>.From(result);
        var goals = result.GetValue<List<Goal>>() ?? new List<Goal>();
        var typed = data.@this<global::app.type.list.@this<Goal>>.Ok(global::app.type.list.@this<Goal>.Of(goals));
        typed.Warnings = result.Warnings;   // forward builder warnings (corrupt .pr, etc.)
        return typed;
    }
}
