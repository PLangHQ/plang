namespace App.modules.list;

[Action("count")]
public partial class Count : IContext
{
    [VariableName]
    public partial string ListName { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName);
        var countData = data.GetChild("Count");

        if (countData.IsInitialized && countData.Value is int c)
            return Task.FromResult(Data(c));

        // Fallback: enumerate
        int count = 0;
        foreach (var _ in data.EnumerateItems()) count++;
        return Task.FromResult(Data(count));
    }
}
