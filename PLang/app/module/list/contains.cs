using app.variable;

namespace app.module.list;

[Action("contains")]
public partial class Contains : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    public partial data.@this Value { get; init; }

    public async Task<data.@this<global::app.type.@bool.@this>> Run()
    {
        var data = Context.Variable.Get(await ListName.Value());
        var target = await Value.Value();

        foreach (var (_, item) in data.EnumerateItems())
        {
            if (global::app.data.Compare.AreEqualValues(await item.Value(), target))
                return global::app.data.@this<global::app.type.@bool.@this>.Ok(true);
        }

        return global::app.data.@this<global::app.type.@bool.@this>.Ok(false);
    }
}
