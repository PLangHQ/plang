namespace app.goal.step.action;

// The action finishes ITSELF at build. The walk is the node's, like Run and Validate: it binds its
// handler (typed views, nothing resolved), lets the handler judge and finish its own properties, then
// walks what it holds — its modifiers, its recovery, the steps of its branch body. The builder calls
// the chain and reacts to what comes back.
public partial class @this
{
    /// <summary>Binds this action's handler and runs its build-time hooks — <c>Validate()</c>, then
    /// <c>Build()</c> — then does the same for every action it holds: one held in a parameter (a
    /// callback), its modifiers, its recovery, the steps of its branch body. Null when nothing is wrong;
    /// otherwise one error naming this action, with each finding as a cause.
    /// <para>A handler's <c>Build()</c> result is published as <c>%!buildData%</c> — the handle the
    /// next action's <c>Build()</c> reads to see what it captures (build-scoped, so it never clobbers
    /// the runtime <c>%!data%</c> of the actor running the builder).</para></summary>
    public async System.Threading.Tasks.Task<global::app.error.IError?> Build(
        global::app.actor.context.@this context)
    {
        var causes = new System.Collections.Generic.List<global::app.error.IError>();

        var (handler, bindError) = await Bind(context);
        if (bindError != null)
            causes.Add(bindError);
        else if (handler is global::app.module.IClass own)
        {
            if (await own.Validate() is { } complaint)
                causes.Add(complaint);
            else
            {
                var built = await own.Build();
                if (!built.Success) causes.Add(built.Error ?? new global::app.error.Error("Build() failed", "BuildFailed", 400));
                else await context.Variable.Set("!buildData", built);
            }
        }

        foreach (var parameter in Parameter)
            if (parameter.Peek() is @this held && await held.Build(context) is { } heldFailed) causes.Add(heldFailed);
        foreach (var modifier in Modifier)
            if (await modifier.Build(context) is { } invalid) causes.Add(invalid);
        if (await Recovery.Build(context) is { } recovery) causes.Add(recovery);
        for (int i = 0; i < Child.Count; i++)
            if (await Child[i].Action.Build(context) is { } branch) causes.Add(branch);

        if (causes.Count == 0) return null;
        return new global::app.error.Error(
            $"{Module}.{Name}: {string.Join("; ", causes.Select(c => c.Message))}", "BuildFailed", 400)
        {
            Action = this,
            list = causes,
        };
    }
}
