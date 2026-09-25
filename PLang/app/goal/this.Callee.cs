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
}
