using app.variable;
using app.module.build.code;
using Goal = app.goal.@this;

namespace app.module.build;

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
        var goals = global::app.type.item.@this.Lower<List<Goal>>(await result.Value()) ?? new List<Goal>();
        var rows = goals.Select(g => new data.@this("", g, context: Context));
        var typed = Context.Ok<global::app.type.list.@this<Goal>>(
            new global::app.type.list.@this<Goal>(rows, Context));
        typed.Warnings = result.Warnings;   // forward builder warnings (corrupt .pr, etc.)
        return typed;
    }
}
