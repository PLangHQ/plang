using app.Variables;

namespace app.modules.list;

[System.ComponentModel.Description("Split a string into a list using a separator (default comma), optionally removing empty entries")]
[Action("split")]
public partial class Split : IContext
{
    public partial data.@this<string> Value { get; init; }
    [Default(",")]
    public partial data.@this<string> Separator { get; init; }
    [Default(false)]
    public partial data.@this<bool> RemoveEmpty { get; init; }

    public Task<data.@this> Run()
    {
        var options = RemoveEmpty.Value
            ? StringSplitOptions.RemoveEmptyEntries
            : StringSplitOptions.None;

        var parts = Value.Value!.Split(new[] { Separator.Value! }, options);
        var list = parts.Cast<object?>().ToList();

        return Task.FromResult(Data(list, app.data.type.FromName("list")));
    }
}
