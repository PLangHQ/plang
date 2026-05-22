using app.variables;
using app.types;

namespace app.modules.file;

[System.ComponentModel.Description("Write Value to a file at Path, creating directories as needed")]
[Action("save", Cacheable = false)]
public partial class Save : IContext
{
    public partial data.@this<global::app.types.path.@this> Path { get; init; }
    public partial data.@this? Value { get; init; }

    public async Task<data.@this> Run()
    {
        if (Path.Value is global::app.types.path.file.@this fp)
            return await fp.Save(Value);
        var raw = Value?.Value;
        if (raw is byte[] bytes) return await Path.Value!.WriteBytes(bytes);
        return await Path.Value!.WriteText(raw?.ToString() ?? "");
    }
}
