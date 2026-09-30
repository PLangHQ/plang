using app.type;

namespace app.module.file;

[Action("copy", Cacheable = false)]
public partial class Copy : IContext, IWrite
{
    public partial data.@this<path> Source { get; init; }
    public partial data.@this<path> Destination { get; init; }

    data.@this<path> IWrite.Target => Destination;

    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Overwrite { get; init; }

    [Default(true)]
    public partial data.@this<global::app.type.item.@bool.@this> IncludeSubfolders { get; init; }

    // A path that didn't resolve (an unregistered s3://) is the answer — its own SchemeNotRegistered.
    public async Task<data.@this<path>> Start() => data.@this<path>.From(
        await Source.Use(source => Destination.Use(destination => Overwrite.Use(overwrite => IncludeSubfolders.Use(async includeSubfolders =>
            (data.@this)await source.CopyTo(destination, overwrite, includeSubfolders, Context))))));
}
