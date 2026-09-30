using app.type;

namespace app.module.file;

[Action("save", Cacheable = false)]
public partial class Save : IContext, IWrite
{
    public partial data.@this<path> Path { get; init; }
    public partial data.@this Value { get; init; }

    data.@this<path> IWrite.Target => Path;

    public async Task<data.@this<path>> Start() => data.@this<path>.From(
        await Path.Use(async path => (data.@this)await path.Save(Value, Context)));
}
