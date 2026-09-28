namespace app.type.item.number;

public sealed partial class @this
{
    /// <summary>The numbers from this one to <paramref name="to"/>, <paramref name="step"/> apart (counting down
    /// when the step is negative) — <c>list.range</c>'s list. A step of zero is InvalidStep; a bound or a step
    /// that didn't resolve is its own answer. A new list, born through its type.</summary>
    public global::System.Threading.Tasks.Task<global::app.data.@this> Range(global::app.data.@this<@this> to,
        global::app.data.@this<@this> step, global::app.actor.context.@this context)
        => to.Use(end => step.Use(async by =>
        {
            if (by == 0)
                return context.Error(new global::app.error.ValidationError("Step cannot be zero", "InvalidStep"));
            var numbers = new global::System.Collections.Generic.List<long>();
            long first = ToInt64(), last = end.ToInt64(), stride = by.ToInt64();
            for (var n = first; stride > 0 ? n <= last : n >= last; n += stride) numbers.Add(n);
            return await context.App.type.list["list"].Create(numbers, context);
        }));
}
