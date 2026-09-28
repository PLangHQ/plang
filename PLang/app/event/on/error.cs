namespace app.@event.on;

/// <summary>An error: what runs when the thing it is on fails — the outcome of an action's attempt, answered by the
/// <c>on.error</c> clauses bound on it (on its <c>before</c> side, in the order they were written).</summary>
public sealed class error : global::app.@event.@this
{
    internal error(binding.list.before before, binding.list.after after) : base("error", before, after) { }

    protected override global::app.@event.@this Of(global::app.@event.on.@this on) => on.error;

    /// <summary>
    /// The failure <paramref name="failed"/> handed to what is bound on <paramref name="item"/>'s error, level by
    /// level, in the order added: the first binding that takes it answers — anything but the failure handed back
    /// (a retry's result, a recovery's, an ignore, or a new failure) — and the rest never see it. A failure no
    /// binding takes stands as it was.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<global::app.data.@this> Catch(global::app.type.item.@this item,
        global::app.data.@this failed, global::app.actor.context.@this context)
    {
        if (!IsBound(item, context)) return failed;
        for (var depth = 0; item.Level(depth, context) is { } level; depth++)
            foreach (var binding in Of(level.on).before)
            {
                if (!binding.For(item, context)) continue;
                var answer = await binding.Start(item, failed, context);
                if (!ReferenceEquals(answer, failed)) return answer;
            }
        return failed;
    }
}
