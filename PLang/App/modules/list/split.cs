using App.Variables;

namespace App.modules.list;

[System.ComponentModel.Description("Split a string into a list using a separator (default comma), optionally removing empty entries")]
[Action("split")]
public partial class Split : IContext
{
    public partial Data.@this<string> Value { get; init; }
    [Default(",")]
    public partial Data.@this<string> Separator { get; init; }
    [Default(false)]
    public partial Data.@this<bool> RemoveEmpty { get; init; }

    public Task<Data.@this> Run()
    {
        var options = RemoveEmpty.Value
            ? StringSplitOptions.RemoveEmptyEntries
            : StringSplitOptions.None;

        var parts = Value.Value!.Split(new[] { Separator.Value! }, options);
        var list = parts.Cast<object?>().ToList();

        return Task.FromResult(Data(list, App.Data.Type.FromName("list")));
    }
}
