using app.variable;

namespace app.module.list;

[Action("contains")]
public partial class Contains : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    public partial data.@this Value { get; init; }

    public Task<data.@this<global::app.type.@bool.@this>> Run()
    {
        var data = Context.Variable.Get(ListName.Value);
        var target = Value.Value;

        foreach (var (_, item) in data.EnumerateItems())
        {
            if (global::app.data.Compare.AreEqualValues(item.Value, target))
                return Task.FromResult(global::app.data.@this<global::app.type.@bool.@this>.Ok(true));
        }

        return Task.FromResult(global::app.data.@this<global::app.type.@bool.@this>.Ok(false));
    }
}
