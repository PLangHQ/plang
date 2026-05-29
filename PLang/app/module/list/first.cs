using app.variable;

namespace app.module.list;

[Action("first")]
public partial class First : IContext
{
    public partial data.@this<Variable> ListName { get; init; }

    public Task<data.@this<object>> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        var first = data.GetChild("[0]");

        return Task.FromResult(first.IsInitialized ? global::app.data.@this<object>.Ok(first.Value) : new global::app.data.@this<object>());
    }
}
