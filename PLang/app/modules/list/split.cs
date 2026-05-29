using app.variable;

namespace app.modules.list;

[Action("split")]
public partial class Split : IContext
{
    public partial data.@this<string> Value { get; init; }
    [Default(",")]
    public partial data.@this<string> Separator { get; init; }
    [Default(false)]
    public partial data.@this<bool> RemoveEmpty { get; init; }

    public Task<data.@this<types.list>> Run()
    {
        var options = RemoveEmpty.Value
            ? StringSplitOptions.RemoveEmptyEntries
            : StringSplitOptions.None;

        var parts = Value.Value!.Split(new[] { Separator.Value! }, options);
        var list = parts.Cast<object?>().ToList();

        return Task.FromResult(global::app.data.@this<types.list>.Ok(
            new types.list { count = list.Count, value = list }, app.data.type.FromName("list")));
    }
}
