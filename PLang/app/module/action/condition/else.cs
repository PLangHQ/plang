using app;

namespace app.module.action.condition;

[Action("else")]
public partial class Else : IContext, IStep
{
    public Task<data.@this<global::app.type.item.@bool.@this>> Start() => Task.FromResult(Context.Ok<global::app.type.item.@bool.@this>(true));
}
