using app.variable;

namespace app.module.action.list;

[Action("count")]
public partial class Count : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public async Task<data.@this<global::app.type.item.number.@this>> Run()
    {
        var data = await Context.Variable.Get((await ListName.Value()));
        var countData = await data.Get("Count");

        // The typed surface answers in a `number` (raw int covers IList infra).
        var counted = countData.IsInitialized ? await countData.Value() : null;
        if (counted is global::app.type.item.number.@this n)
            return Context.Ok<global::app.type.item.number.@this>(n);

        // Fallback: enumerate
        int count = 0;
        foreach (var _ in await data.EnumerateItems()) count++;
        return Context.Ok<global::app.type.item.number.@this>(count);
    }
}
