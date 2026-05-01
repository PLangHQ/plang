using App.Variables;

namespace App.modules.list;

[System.ComponentModel.Description("Return the number of items in the list")]
[Action("count")]
public partial class Count : IContext
{
    public partial Data.@this<Variable> ListName { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        var countData = data.GetChild("Count");

        if (countData.IsInitialized && countData.Value is int c)
            return Task.FromResult(Data(c));

        // Fallback: enumerate
        int count = 0;
        foreach (var _ in data.EnumerateItems()) count++;
        return Task.FromResult(Data(count));
    }
}
