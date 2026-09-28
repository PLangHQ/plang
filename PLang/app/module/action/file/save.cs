using app.type;

namespace app.module.action.file;

[Action("save", Cacheable = false)]
public partial class Save : IContext
{
    public partial data.@this<path> Path { get; init; }
    public partial data.@this Value { get; init; }

    public async Task<data.@this<path>> Start() => data.@this<path>.From(
        await Path.Use(async path => (data.@this)await path.Save(Value, Context)));
}
