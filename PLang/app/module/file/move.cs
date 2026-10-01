using app.type;

namespace app.module.file;

[Action("move", Cacheable = false)]
public partial class Move : IContext
{
    public partial data.@this<path> Source { get; init; }
    public partial data.@this<path> Destination { get; init; }

    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Overwrite { get; init; }

    // A path that didn't resolve (an unregistered s3://) is the answer — its own SchemeNotRegistered.
    public async Task<data.@this<path>> Start() => data.@this<path>.From(
        await Source.Use(source => Destination.Use(destination => Overwrite.Use(async overwrite =>
            (data.@this)await source.MoveTo(destination, overwrite, Context)))));

    /// <summary>In the build's files the source is gone and the destination there — the file under it when it
    /// names a folder — for the steps after it; a path holding a variable is known only at run.</summary>
    public async Task<data.@this> Build() => Source.HasVariable || Destination.HasVariable ? Context.Ok()
        : await Source.Use(source => Destination.Use(destination =>
        {
            destination.Add(Context, source);
            source.Remove(Context);
            return Task.FromResult(Context.Ok());
        }));
}
