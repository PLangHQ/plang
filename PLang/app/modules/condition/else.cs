using app;

namespace app.modules.condition;

[System.ComponentModel.Description("Fallback branch that executes when all preceding if/elseif conditions are false")]
[Action("else")]
public partial class Else : IContext, IStep
{
    public Task<data.@this<bool>> Run() => Task.FromResult(global::app.data.@this<bool>.Ok(true));
}
