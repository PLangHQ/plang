using app.variable;

namespace app.module.list;

[Action("split")]
public partial class Split : IContext
{
    public partial data.@this<global::app.type.text.@this> Value { get; init; }
    [Default(",")]
    public partial data.@this<global::app.type.text.@this> Separator { get; init; }
    [Default(false)]
    public partial data.@this<global::app.type.@bool.@this> RemoveEmpty { get; init; }

    public Task<data.@this<type.list>> Run()
    {
        var options = RemoveEmpty.Value
            ? StringSplitOptions.RemoveEmptyEntries
            : StringSplitOptions.None;

        var parts = ((string)Value.Value!).Split(new[] { (string)Separator.Value! }, options);
        var list = new app.type.list.@this { Context = Context };
        foreach (var part in parts)
            list.Add(new global::app.data.@this("", part));

        return Task.FromResult(global::app.data.@this<type.list>.Ok(
            new type.list { count = list.Count, value = list }, app.type.@this.FromName("list")));
    }
}
