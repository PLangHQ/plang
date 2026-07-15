using app.variable;

namespace app.module.action.list;

[Action("contains")]
public partial class Contains : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    public partial data.@this Value { get; init; }

    public async Task<data.@this<global::app.type.item.@bool.@this>> Run()
    {
        var data = await Context.Variable.Get(await ListName.Value());

        // Membership through THE comparison entry: matches only on Equal, never
        // errors — a mixed list treats NotEqual/Incomparable as "not this one".
        foreach (var (_, item) in await data.EnumerateItems())
        {
            if (await item.Compare(Value) == global::app.data.Comparison.Equal)
                return Context.Ok<global::app.type.item.@bool.@this>(true);
        }

        return Context.Ok<global::app.type.item.@bool.@this>(false);
    }
}
