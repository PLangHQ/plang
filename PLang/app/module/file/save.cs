using app.type;

namespace app.module.file;

[Action("save", Cacheable = false)]
public partial class Save : IContext
{
    public partial data.@this<path> Path { get; init; }
    public partial data.@this Value { get; init; }

    public async Task<data.@this<path>> Start() => data.@this<path>.From(
        await Path.Use(async path => (data.@this)await path.Save(Value, Context)));

    /// <summary>The file it saves is in the build's files, for the steps after it; a path holding a variable
    /// is known only at run.</summary>
    public async Task<data.@this> Build() => Path.HasVariable ? Context.Ok() : await Path.Use(path =>
    {
        path.Add(Context);
        return Task.FromResult(Context.Ok());
    });
}
