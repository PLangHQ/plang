namespace app.goal;

public sealed partial class @this
{
    /// <summary>Every goal this goal reaches through its calls — the goals its actions call, the goals
    /// those call, and so on, each once, in the order first reached. This goal itself is never among them,
    /// even when a call leads back to it.</summary>
    public async System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<@this>> Callee(
        global::app.actor.context.@this context)
    {
        var reached = new System.Collections.Generic.List<@this>();
        var seen = new System.Collections.Generic.HashSet<@this>(System.Collections.Generic.ReferenceEqualityComparer.Instance) { this };
        var pending = new System.Collections.Generic.Queue<@this>();
        pending.Enqueue(this);

        while (pending.Count > 0)
            foreach (var step in pending.Dequeue().Step.Items())
                foreach (var action in step.Code.Items())
                    foreach (var goal in await action.Callee(context))
                        if (seen.Add(goal))
                        {
                            reached.Add(goal);
                            pending.Enqueue(goal);
                        }

        return reached;
    }

    /// <summary>The private goals of this file that nothing reaches. A private goal is called only from
    /// its own file, so what reaches it is this goal — the file's public one — and what that calls. None
    /// when this is a private goal, and none when a call in the file names its goal only at run: that call
    /// could name any goal of the file.</summary>
    public async System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<@this>> Unreached(
        global::app.actor.context.@this context)
    {
        if (Parent != null) return [];
        foreach (var goal in (System.Collections.Generic.IEnumerable<@this>)[this, .. Child.Items()])
            foreach (var step in goal.Step.Items())
                foreach (var action in step.Code.Items())
                    if (await action.IsDynamic(context)) return [];

        var reached = await Callee(context);
        return Child.Items().Where(sub => !reached.Any(goal => ReferenceEquals(goal, sub))).ToList();
    }
}
