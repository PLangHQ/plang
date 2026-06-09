using app.variable;

namespace app.module.list;

[Action("indexof")]
public partial class IndexOf : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    public partial data.@this Value { get; init; }

    public Task<data.@this<global::app.type.number.@this>> Run()
    {
        var data = Context.Variable.Get(ListName.Materialize() as app.variable.@this);
        var target = Value.Materialize();

        foreach (var (key, item) in data.EnumerateItems())
        {
            if (global::app.data.Compare.AreEqualValues(item.Materialize(), target))
                return Task.FromResult(global::app.data.@this<global::app.type.number.@this>.Ok(Convert.ToInt32(key.Materialize())));
        }

        return Task.FromResult(global::app.data.@this<global::app.type.number.@this>.Ok(-1));
    }
}
