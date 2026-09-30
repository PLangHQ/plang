using app.type;

namespace app.module.file;

[Action("copy", Cacheable = false)]
public partial class Copy : IContext
{
    public partial data.@this<path> Source { get; init; }
    public partial data.@this<path> Destination { get; init; }

    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Overwrite { get; init; }

    [Default(true)]
    public partial data.@this<global::app.type.item.@bool.@this> IncludeSubfolders { get; init; }

    // A path that didn't resolve (an unregistered s3://) is the answer — its own SchemeNotRegistered.
    public async Task<data.@this<path>> Start() => data.@this<path>.From(
        await Source.Use(source => Destination.Use(destination => Overwrite.Use(overwrite => IncludeSubfolders.Use(async includeSubfolders =>
            (data.@this)await source.CopyTo(destination, overwrite, includeSubfolders, Context))))));

    /// <summary>The copy is in the build's files, for the steps after it — the file under the destination when
    /// that names a folder; a path holding a variable is known only at run.</summary>
    public async Task<data.@this> Build() => Source.HasVariable || Destination.HasVariable ? Context.Ok()
        : await Source.Use(source => Destination.Use(destination =>
        {
            if (source is global::app.type.item.path.file.@this from && destination is global::app.type.item.path.file.@this to
                && Context.App.Build?.Files is { } files)
                files.Add(files.IsFolder(to) ? new global::app.type.item.path.file.@this(to.Combine(from.FileName).Absolute) : to);
            return Task.FromResult(Context.Ok());
        }));
}
