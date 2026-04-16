namespace App.modules.list;

[Action("get")]
public partial class Get : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    public partial Data.@this<int> Index { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName);
        var item = data.GetChild($"[{Index.Value}]");

        if (!item.IsInitialized)
            return Task.FromResult(Error(
                new App.Errors.ValidationError($"Index {Index.Value} out of range for '{ListName}'")));

        return Task.FromResult(Data(item.Value));
    }
}
