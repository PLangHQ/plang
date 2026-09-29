namespace app.goal.step.action;

// The goals an action calls. The walk is the node's, like Build: it binds its handler and asks it,
// then walks what it holds — the actions its properties hold (a callback, a recovery), the steps of its
// branch body.
public partial class @this
{
    /// <summary>The goals this action and every action it holds call, as their properties name them
    /// now. An action whose handler does not bind calls nothing.</summary>
    public async System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<global::app.goal.@this>> Callee(
        global::app.actor.context.@this context)
    {
        var callee = new System.Collections.Generic.List<global::app.goal.@this>();

        var (handler, _) = await Bind(context);
        if (handler is global::app.module.IClass own && await own.Callee() is { } goal)
            callee.Add(goal);

        foreach (var held in Held)
            callee.AddRange(await held.Callee(context));
        for (int i = 0; i < Child.Count; i++)
            foreach (var branch in Child[i].Code.Items())
                callee.AddRange(await branch.Callee(context));

        return callee;
    }

    /// <summary>True when this action, or an action it holds, calls a goal named only at run — a goal
    /// <see cref="Callee"/> cannot name now.</summary>
    public async System.Threading.Tasks.Task<bool> IsDynamic(global::app.actor.context.@this context)
    {
        var (handler, _) = await Bind(context);
        if (handler is global::app.module.IClass { IsDynamic: true }) return true;

        foreach (var held in Held)
            if (await held.IsDynamic(context)) return true;
        for (int i = 0; i < Child.Count; i++)
            foreach (var branch in Child[i].Code.Items())
                if (await branch.IsDynamic(context)) return true;

        return false;
    }
}
