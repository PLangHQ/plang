using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.list;

[Action("range")]
public partial class Range : IContext
{
    public partial int Start { get; init; }
    public partial int End { get; init; }
    [Default(1)]
    public partial int Step { get; init; }

    public Task<Data> Run()
    {
        if (Step == 0)
            return Task.FromResult(Data.FromError(
                new Errors.ValidationError("Step cannot be zero", "InvalidStep")));

        var list = new List<object?>();
        if (Step > 0)
        {
            for (int i = Start; i <= End; i += Step)
                list.Add(i);
        }
        else
        {
            for (int i = Start; i >= End; i += Step)
                list.Add(i);
        }

        return Task.FromResult(Data.Ok(new types.list { count = list.Count, value = list }, Memory.Type.FromName("list")));
    }
}
