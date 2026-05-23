using app.variables;

namespace app.modules.list;

[Action("count")]
public partial class Count : IContext
{
    public partial data.@this<Variable> ListName { get; init; }

    public Task<data.@this<int>> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        var countData = data.GetChild("Count");

        if (countData.IsInitialized && countData.Value is int c)
            return Task.FromResult(global::app.data.@this<int>.Ok(c));

        // Fallback: enumerate
        int count = 0;
        foreach (var _ in data.EnumerateItems()) count++;
        return Task.FromResult(global::app.data.@this<int>.Ok(count));
    }
}
