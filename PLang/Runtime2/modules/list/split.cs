using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.list;

[Action("split")]
public partial class Split : IContext
{
    public partial string Value { get; init; }
    [Default(",")]
    public partial string Separator { get; init; }
    [Default(false)]
    public partial bool RemoveEmpty { get; init; }

    public Task<Data> Run()
    {
        var options = RemoveEmpty
            ? StringSplitOptions.RemoveEmptyEntries
            : StringSplitOptions.None;

        var parts = Value.Split(new[] { Separator }, options);
        var list = parts.Cast<object?>().ToList();

        return Task.FromResult(Data.Ok(list, PLang.Runtime2.Engine.Memory.Type.FromName("list")));
    }
}
