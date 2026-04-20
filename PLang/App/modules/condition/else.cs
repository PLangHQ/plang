using App;

namespace App.modules.condition;

[Example("if %x% > 5 write 'big', else write 'small'", "no parameters")]
[Action("else")]
public partial class Else : IContext, IStep
{
    public Task<Data.@this> Run() => Task.FromResult(Data(true));
}
