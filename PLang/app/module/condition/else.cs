using app;

namespace app.module.condition;

[Action("else")]
public partial class Else : IContext, IStep
{
    public Task<data.@this<global::app.type.@bool.@this>> Run() => Task.FromResult(Context.Ok<global::app.type.@bool.@this>(true));
}
