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

    public async Task<data.@this<global::app.type.item.list.@this<global::app.type.clr.@this<Goal>>>> Run()
    {
        var result = await Builder.Goals(this);
        if (!result.Success) return data.@this<global::app.type.item.list.@this<global::app.type.clr.@this<Goal>>>.From(result);
        var goals = (await result.Value()).Clr<List<Goal>>() ?? new List<Goal>();
        // goal is a host now — each rides the plang list as clr<goal>.
        var carried = new List<global::app.type.clr.@this<Goal>>(goals.Count);
        foreach (var g in goals) carried.Add(new global::app.type.clr.@this<Goal>(g, Context));
        var typed = Context.Ok<global::app.type.item.list.@this<global::app.type.clr.@this<Goal>>>(
            new global::app.type.item.list.@this<global::app.type.clr.@this<Goal>>(carried, Context));
        typed.Warnings = result.Warnings;   // forward builder warnings (corrupt .pr, etc.)
        return typed;
    }
}
