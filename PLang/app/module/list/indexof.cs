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
        var target = (await Value.Value());

        foreach (var (key, item) in data.EnumerateItems())
        {
            if (global::app.data.Compare.AreEqualValues((await item.Value()), target))
                return global::app.data.@this<global::app.type.number.@this>.Ok(Convert.ToInt32((await key.Value())));
        }

        return global::app.data.@this<global::app.type.number.@this>.Ok(-1);
    }
}
