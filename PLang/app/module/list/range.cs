using app.variable;

namespace app.module.list;

[Action("range")]
public partial class Range : IContext
{
    public partial data.@this<int> Start { get; init; }
    public partial data.@this<int> End { get; init; }
    [Default(1)]
    public partial data.@this<int> Step { get; init; }

    public Task<data.@this<type.list>> Run()
    {
        if (Step.Value == 0)
            return Task.FromResult(global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError("Step cannot be zero", "InvalidStep")));

        var list = new app.type.list.@this { Context = Context };
        if (Step.Value > 0)
        {
            for (int i = Start.Value; i <= End.Value; i += Step.Value)
                list.Add(new global::app.data.@this("", i));
        }
        else
        {
            for (int i = Start.Value; i >= End.Value; i += Step.Value)
                list.Add(new global::app.data.@this("", i));
        }

        return Task.FromResult(global::app.data.@this<type.list>.Ok(new type.list { count = list.Count, value = list }, app.type.@this.FromName("list")));
    }
}
