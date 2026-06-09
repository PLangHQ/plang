using app.variable;

namespace app.module.list;

[Action("count")]
public partial class Count : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public async Task<data.@this<global::app.type.number.@this>> Run()
    {
        var data = await Context.Variable.Get((await ListName.Value()));
        var countData = data.GetChild("Count");

        if (countData.IsInitialized && (await countData.Value()) is int c)
            return global::app.data.@this<global::app.type.number.@this>.Ok(c);

        // Fallback: enumerate
        int count = 0;
        foreach (var _ in data.EnumerateItems()) count++;
        return global::app.data.@this<global::app.type.number.@this>.Ok(count);
    }
}
