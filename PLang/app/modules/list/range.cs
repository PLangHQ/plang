using app.variables;

namespace app.modules.list;

[Action("range")]
public partial class Range : IContext
{
    public partial data.@this<int> Start { get; init; }
    public partial data.@this<int> End { get; init; }
    [Default(1)]
    public partial data.@this<int> Step { get; init; }

    public Task<data.@this<types.list>> Run()
    {
        if (Step.Value == 0)
            return Task.FromResult(global::app.data.@this<types.list>.FromError(
                new app.error.ValidationError("Step cannot be zero", "InvalidStep")));

        var list = new List<object?>();
        if (Step.Value > 0)
        {
            for (int i = Start.Value; i <= End.Value; i += Step.Value)
                list.Add(i);
        }
        else
        {
            for (int i = Start.Value; i >= End.Value; i += Step.Value)
                list.Add(i);
        }

        return Task.FromResult(global::app.data.@this<types.list>.Ok(new types.list { count = list.Count, value = list }, app.data.type.FromName("list")));
    }
}
