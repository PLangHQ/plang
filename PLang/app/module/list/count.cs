using app.variable;

namespace app.module.list;

[Action("count")]
public partial class Count : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public Task<data.@this<global::app.type.number.@this>> Run()
    {
        var data = Context.Variable.Get(ListName.Value);
        var countData = data.GetChild("Count");

        if (countData.IsInitialized && countData.Value is int c)
            return Task.FromResult(global::app.data.@this<global::app.type.number.@this>.Ok(c));

        // Fallback: enumerate
        int count = 0;
        foreach (var _ in data.EnumerateItems()) count++;
        return Task.FromResult(global::app.data.@this<global::app.type.number.@this>.Ok(count));
    }
}
