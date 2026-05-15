using app.Variables;

namespace app.modules.list;

[System.ComponentModel.Description("Return the item at a zero-based Index from the list")]
[Action("get")]
public partial class Get : IContext
{
    public partial data.@this<Variable> ListName { get; init; }
    public partial data.@this<int> Index { get; init; }

    public Task<data.@this> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        var item = data.GetChild($"[{Index.Value}]");

        if (!item.IsInitialized)
            return Task.FromResult(Error(
                new app.Errors.ValidationError($"Index {Index.Value} out of range for '{ListName.Value}'")));

        return Task.FromResult(Data(item.Value));
    }
}
