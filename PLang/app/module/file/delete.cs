using app.type;

namespace app.module.file;

[Action("delete", Cacheable = false)]
public partial class Delete : IContext
{
    public partial data.@this<path> Path { get; init; }

    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> IgnoreIfNotFound { get; init; }

    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Recursive { get; init; }

    public async Task<data.@this<path>> Start() => data.@this<path>.From(
        await Path.Use(path => Recursive.Use(recursive => IgnoreIfNotFound.Use(async ignoreIfNotFound =>
            (data.@this)await path.Delete(recursive, ignoreIfNotFound, Context)))));

    /// <summary>The file or folder is gone from the build's files, for the steps after it; a path holding a
    /// variable is known only at run.</summary>
    public async Task<data.@this> Build() => Path.HasVariable ? Context.Ok() : await Path.Use(path =>
    {
        path.Remove(Context);
        return Task.FromResult(Context.Ok());
    });
}
