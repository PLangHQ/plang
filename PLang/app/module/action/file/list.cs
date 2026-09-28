using app.type;

namespace app.module.action.file;

[Action("list")]
public partial class List : IContext
{
    public partial data.@this<path> Path { get; init; }

    [Default("*")]
    public partial data.@this<global::app.type.item.text.@this> Pattern { get; init; }

    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Recursive { get; init; }

    public async Task<data.@this<global::app.type.item.list.@this<path>>> Start() => data.@this<global::app.type.item.list.@this<path>>.From(
        await Path.Use(path => Pattern.Use(pattern => Recursive.Use(async recursive =>
            (data.@this)await path.List(pattern, recursive, Context)))));
}
