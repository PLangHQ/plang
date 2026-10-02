using app.type;

namespace app.module.file;

[Action("copy", Cacheable = false)]
public partial class Copy : IContext
{
    public partial data.@this<path> Source { get; init; }
    public partial data.@this<path> Destination { get; init; }

    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Overwrite { get; init; }

    /// <summary>Whether a folder's subfolders are copied with it — included, unless the step skips them.</summary>
    [Default(global::app.module.file.type.subfolder.include)]
    public partial data.@this<global::app.type.item.choice.@this<global::app.module.file.type.subfolder>> Subfolder { get; init; }

    // A path that didn't resolve (an unregistered s3://) is the answer — its own SchemeNotRegistered.
    public async Task<data.@this<path>> Start() => data.@this<path>.From(
        await Source.Use(source => Destination.Use(destination => Overwrite.Use(overwrite => Subfolder.Use(async under =>
            (data.@this)await source.CopyTo(destination, overwrite, under.Value == global::app.module.file.type.subfolder.include, Context))))));

    /// <summary>The copy is in the build's files, for the steps after it — the file under the destination when
    /// that names a folder; a path holding a variable is known only at run.</summary>
    public async Task<data.@this> Build() => Source.HasVariable || Destination.HasVariable ? Context.Ok()
        : await Source.Use(source => Destination.Use(destination =>
        {
            destination.Add(Context, source);
            return Task.FromResult(Context.Ok());
        }));
}
