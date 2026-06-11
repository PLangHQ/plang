using app.variable;

namespace app.module.list;

[Action("count")]
public partial class Count : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public async Task<data.@this<global::app.type.number.@this>> Run()
    {
        var data = await Context.Variable.Get((await ListName.Value()));
        var countData = await data.GetChild("Count");

        // The typed surface answers in a `number` (raw int covers IList infra).
        var counted = countData.IsInitialized ? await countData.Value() : null;
        if (counted is global::app.type.number.@this n)
            return global::app.data.@this<global::app.type.number.@this>.Ok(n);

        // Fallback: enumerate
        int count = 0;
        foreach (var _ in data.EnumerateItems()) count++;
        return global::app.data.@this<global::app.type.number.@this>.Ok(count);
    }
}
