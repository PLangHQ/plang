using app.variables;
using app.types;

namespace app.modules.file;

[Action("move", Cacheable = false)]
public partial class Move : IContext
{
    public partial data.@this<path> Source { get; init; }
    public partial data.@this<path> Destination { get; init; }

    [Default(false)]
    public partial data.@this<bool> Overwrite { get; init; }

    public async Task<data.@this<path>> Run()
    {
        // typed scheme error, not an NRE on .Value.
        if (!Source.Success) return data.@this<path>.From(Source);
        if (!Destination.Success) return data.@this<path>.From(Destination);
        return await Source.Value!.MoveTo(Destination.Value!, Overwrite.Value);
    }
}
