using app.variable;
using app.type;

namespace app.module.action.file;

[Action("copy", Cacheable = false)]
public partial class Copy : IContext
{
    public partial data.@this<path> Source { get; init; }
    public partial data.@this<path> Destination { get; init; }

    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Overwrite { get; init; }

    [Default(true)]
    public partial data.@this<global::app.type.item.@bool.@this> IncludeSubfolders { get; init; }

    public async Task<data.@this<path>> Run()
    {
        // Failed scheme resolution (e.g. unregistered s3://) surfaces the typed
        // SchemeNotRegistered error instead of an NRE on .Value.
        if (!Source.Success) return data.@this<path>.From(Source);
        if (!Destination.Success) return data.@this<path>.From(Destination);
        return await (await Source.Value())!.CopyTo((await Destination.Value())!, (await Overwrite.Value())!.Value, (await IncludeSubfolders.Value())!.Value);
    }
}
