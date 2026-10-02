namespace app.goal.step.action;

// The goals an action calls: the goals its goal-typed properties name — whatever the action — then the goals the
// actions it holds call (a callback, a recovery) and the steps of its branch body.
public partial class @this
{
    /// <summary>The goals this action and every action it holds call, as their goal-typed properties name them
    /// now — each selected from the goal this action sits in. A %variable% name names none now.</summary>
    public async System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<global::app.goal.@this>> Callee(
        global::app.actor.context.@this context)
    {
        var callee = new System.Collections.Generic.List<global::app.goal.@this>();

        // each named goal, as written, selected from the goal this action sits in
        foreach (var property in Property)
            if (Declares(property) && property.Value is { HasVariable: false, RawText: { Length: > 0 } key }
                && (await context.App.goal.list.Find(key, Step?.Goal)).Peek() is global::app.goal.@this goal)
                callee.Add(goal);

        foreach (var held in Held)
            callee.AddRange(await held.Callee(context));
        for (int i = 0; i < Child.Count; i++)
            foreach (var branch in Child[i].Code.Items())
                callee.AddRange(await branch.Callee(context));

        return callee;
    }

    /// <summary>True when this action, or an action it holds, calls a goal named only at run — a goal-typed
    /// property holding a %variable%, which <see cref="Callee"/> cannot name now.</summary>
    public async System.Threading.Tasks.Task<bool> IsDynamic(global::app.actor.context.@this context)
    {
        foreach (var property in Property)
            if (Declares(property) && property.Value is { HasVariable: true }) return true;

        foreach (var held in Held)
            if (await held.IsDynamic(context)) return true;
        for (int i = 0; i < Child.Count; i++)
            foreach (var branch in Child[i].Code.Items())
                if (await branch.IsDynamic(context)) return true;

        return false;
    }

    // Is the slot this row fills declared a goal — by the action's own catalog, whatever the row was written as.
    private bool Declares(global::app.type.property.@this property)
        => (Module[Name]?.Property[property.Name]?.Type ?? property.Type).Is("goal");
}
