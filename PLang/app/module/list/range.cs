using app.variable;

namespace app.module.list;

[Action("range")]
public partial class Range : IContext
{
    public partial data.@this<global::app.type.number.@this> Start { get; init; }
    public partial data.@this<global::app.type.number.@this> End { get; init; }
    [Default(1)]
    public partial data.@this<global::app.type.number.@this> Step { get; init; }

    public async Task<data.@this<type.list>> Run()
    {
        // Typed reads; the numbers lower at the loop bounds — the handler's
        // own int boundary.
        var stepN = (await Step.Value())!;
        if (stepN == 0)
            return Context.Error<type.list>(
                new app.error.ValidationError("Step cannot be zero", "InvalidStep"));

        var list = new app.type.list.@this(Context);
        int start = (await Start.Value())!.ToInt32(), end = (await End.Value())!.ToInt32(), step = stepN.ToInt32();
        if (step > 0)
        {
            for (int i = start; i <= end; i += step)
                list.Add(new global::app.data.@this("", i, context: Context));
        }
        else
        {
            for (int i = start; i >= end; i += step)
                list.Add(new global::app.data.@this("", i, context: Context));
        }

        return Context.Ok<type.list>(new type.list { count = list.CountRaw, value = list }, Context.Type.Create("list"));
    }
}
