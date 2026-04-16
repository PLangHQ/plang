namespace App.modules.list;

[Action("last")]
public partial class Last : IContext
{
    [VariableName]
    public partial string ListName { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName);
        var countData = data.GetChild("Count");

        if (countData.IsInitialized && countData.Value is int count && count > 0)
        {
            var last = data.GetChild($"[{count - 1}]");
            if (last.IsInitialized) return Task.FromResult(Data(last.Value));
        }

        return Task.FromResult(Data());
    }
}
