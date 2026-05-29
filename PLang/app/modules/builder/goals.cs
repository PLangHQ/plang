using app.variable;
using app.modules.builder.code;
using Goal = app.goals.goal.@this;

namespace app.modules.builder;

[Action("goals")]
public partial class goals : IContext
{
    public partial data.@this<global::app.types.path.@this> Path { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this<List<Goal>>> Run()
    {
        var result = await Builder.Goals(this);
        return data.@this<List<Goal>>.From(result);
    }
}
