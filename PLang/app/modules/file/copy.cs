using app.variables;
using app.types;

namespace app.modules.file;

[ModuleDescription("Read, write, copy, move, delete, and list files through the configured filesystem abstraction")]
[System.ComponentModel.Description("Copy a file or folder from Source to Destination, optionally overwriting and including subfolders")]
[Action("copy", Cacheable = false)]
public partial class Copy : IContext
{
    public partial data.@this<path> Source { get; init; }
    public partial data.@this<path> Destination { get; init; }

    [Default(false)]
    public partial data.@this<bool> Overwrite { get; init; }

    [Default(true)]
    public partial data.@this<bool> IncludeSubfolders { get; init; }

    public async Task<data.@this> Run()
    {
        if (Source.Value is filepath fp)
            return await fp.CopyTo(Destination.Value!, Overwrite.Value, IncludeSubfolders.Value);
        return await Source.Value!.CopyTo(Destination.Value!);
    }
}
