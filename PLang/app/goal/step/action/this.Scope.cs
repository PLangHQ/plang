namespace app.goal.step.action;

// The action at the build's walk over a scratch store (goal.step.list.Scope): it runs nothing.
public partial class @this
{
    /// <summary>This action at the build's walk. Its handler is bound to <paramref name="scratch"/> and
    /// checks its variable properties against what the store knows (<c>IClass.Check</c>); then an
    /// <c>IScope</c> handler makes its own change to the store, and any other action leaves its
    /// return's empty value as <c>%!data%</c> (a typed null where the type has no empty value). The
    /// steps of its branch body are walked in place, each keeping what it knows. One error per
    /// property the store says is the wrong type; a handler that doesn't bind is left to Build.</summary>
    public async System.Threading.Tasks.Task<System.Collections.Generic.List<global::app.error.Error>> Scope(
        global::app.actor.context.@this scratch)
    {
        var declined = new System.Collections.Generic.List<global::app.error.Error>();
        var (handler, bindError) = await Bind(scratch);
        if (bindError == null && handler is global::app.module.IClass own)
            declined.AddRange(await own.Check());
        if (bindError == null && handler is global::app.module.IScope scope)
            await scope.Scope();
        else
        {
            var returned = Return ?? scratch.App.Type["item"];
            await scratch.Variable.Set("!data", new global::app.data.@this("!data", returned.Empty(scratch), returned, context: scratch));
        }
        for (int i = 0; i < Child.Count; i++)
            declined.AddRange(await Child[i].Scope(scratch));
        return declined;
    }
}
