using App;

namespace App.modules.condition;

[System.ComponentModel.Description("Fallback branch that executes when all preceding if/elseif conditions are false")]
[Action("else")]
public partial class Else : IContext, IStep
{
    public Task<Data.@this> Run() => Task.FromResult(Data(true));
}
