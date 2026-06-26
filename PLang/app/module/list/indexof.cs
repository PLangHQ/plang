using app.variable;

namespace app.module.list;

[Action("indexof")]
public partial class IndexOf : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    public partial data.@this Value { get; init; }

    public async Task<data.@this<global::app.type.number.@this>> Run()
    {
        var data = await Context.Variable.Get((await ListName.Value()));

        // Membership through THE comparison entry: matches only on Equal, never errors.
        foreach (var (key, item) in data.EnumerateItems())
        {
            if (await item.Compare(Value) == global::app.data.Comparison.Equal)
                return Context.Ok<global::app.type.number.@this>(Convert.ToInt32((await key.Value())));
        }

        return Context.Ok<global::app.type.number.@this>(-1);
    }
}
