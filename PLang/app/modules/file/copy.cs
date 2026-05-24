using app.variables;
using app.types;

namespace app.modules.file;

[Action("copy", Cacheable = false)]
public partial class Copy : IContext
{
    public partial data.@this<path> Source { get; init; }
    public partial data.@this<path> Destination { get; init; }

    [Default(false)]
    public partial data.@this<bool> Overwrite { get; init; }

    [Default(true)]
    public partial data.@this<bool> IncludeSubfolders { get; init; }

    public async Task<data.@this<path>> Run()
    {
        // Failed scheme resolution (e.g. unregistered s3://) surfaces the typed
        // SchemeNotRegistered error instead of an NRE on .Value. (codeanalyzer v1 F4)
        if (!Source.Success) return data.@this<path>.From(Source);
        if (!Destination.Success) return data.@this<path>.From(Destination);
        return await Source.Value!.CopyTo(Destination.Value!, Overwrite.Value, IncludeSubfolders.Value);
    }
}
