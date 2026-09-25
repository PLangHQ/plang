namespace app.goal.step.action;

// The goals an action calls. The walk is the node's, like Build: it binds its handler and asks it,
// then walks what it holds — an action held in a property (a callback), its modifiers, its recovery,
// the steps of its branch body.
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

        foreach (var property in Property)
            if (property.Value is @this held) callee.AddRange(await held.Callee(context));
        foreach (var modifier in Modifier)
            callee.AddRange(await modifier.Callee(context));
        foreach (var recovery in Recovery.Items())
            callee.AddRange(await recovery.Callee(context));
        for (int i = 0; i < Child.Count; i++)
            foreach (var branch in Child[i].Code.Items())
                callee.AddRange(await branch.Callee(context));

        return callee;
    }
}
