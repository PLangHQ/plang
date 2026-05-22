using app.variables;
using app.types;

namespace app.modules.file;

[System.ComponentModel.Description("Move or rename a file from Source to Destination, optionally overwriting the target")]
[Action("move", Cacheable = false)]
public partial class Move : IContext
{
    public partial data.@this<path> Source { get; init; }
    public partial data.@this<path> Destination { get; init; }

    [Default(false)]
    public partial data.@this<bool> Overwrite { get; init; }

    public async Task<data.@this> Run()
    {
        if (Source.Value is filepath fp)
            return await fp.MoveTo(Destination.Value!, Overwrite.Value);
        return await Source.Value!.MoveTo(Destination.Value!);
    }
}
