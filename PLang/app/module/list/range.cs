using app.variable;

namespace app.module.list;

[Action("range")]
public partial class Range : IContext
{
    public partial data.@this<global::app.type.number.@this> Start { get; init; }
    public partial data.@this<global::app.type.number.@this> End { get; init; }
    [Default(1)]
    public partial data.@this<global::app.type.number.@this> Step { get; init; }

    public Task<data.@this<type.list>> Run()
    {
        if (Step.GetValue<int>() == 0)
            return Task.FromResult(global::app.data.@this<type.list>.FromError(
                new app.error.ValidationError("Step cannot be zero", "InvalidStep")));

        var list = new app.type.list.@this { Context = Context };
        int start = Start.GetValue<int>(), end = End.GetValue<int>(), step = Step.GetValue<int>();
        if (step > 0)
        {
            for (int i = start; i <= end; i += step)
                list.Add(new global::app.data.@this("", i));
        }
        else
        {
            for (int i = start; i >= end; i += step)
                list.Add(new global::app.data.@this("", i));
        }

        return Task.FromResult(global::app.data.@this<type.list>.Ok(new type.list { count = list.CountRaw, value = list }, app.type.@this.FromName("list")));
    }
}
