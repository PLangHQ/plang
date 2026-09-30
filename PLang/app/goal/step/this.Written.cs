namespace app.goal.step;

// The step's place in its goal at build: what the steps before it leave on disk.
public sealed partial class @this
{
    /// <summary>Whether a step before this one in its goal writes <paramref name="location"/> — a goal that saves a
    /// file, then reads it: the read finds it there at run, though it isn't there at build. Only a literal location
    /// the earlier action writes compares; a <c>%variable%</c> one is known at run.</summary>
    internal async System.Threading.Tasks.Task<bool> IsWrittenBefore(global::app.type.item.path.@this location,
        global::app.actor.context.@this context)
    {
        if (Goal == null) return false;
        foreach (var earlier in Goal.Step.Items().Where(s => s.Index < Index))
            foreach (var action in earlier.Code.Items())
                if ((await action.Bind(context)).Handler is global::app.module.IWrite { Target: { HasVariable: false } target }
                    && await target.Value() is { } written && written.Equals(location))
                    return true;
        return false;
    }
}
