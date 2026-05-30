using app;

namespace app.module.condition;

[Action("else")]
public partial class Else : IContext, IStep
{
    public Task<data.@this<bool>> Run() => Task.FromResult(global::app.data.@this<bool>.Ok(true));
}
