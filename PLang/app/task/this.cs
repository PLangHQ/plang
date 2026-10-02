namespace app.task;

/// <summary>
/// A goal call running on its own — what <c>call X in parallel</c> answers. Born only by its actor's list
/// (<see cref="list.@this.Start"/>), which holds it while it runs and lets it go when it ends; the <c>%task%</c> a
/// step wrote it to keeps it after. Its result is reached through <see cref="Wait"/>, one door. A failure nobody asked
/// for by the time it ends goes to its actor's error channel; a later <see cref="Wait"/> still answers it.
/// </summary>
[global::app.Attributes.PlangType("task")]
public sealed class @this : global::app.type.item.@this
{
    private readonly global::app.actor.@this _actor;
    private readonly System.Threading.CancellationTokenSource _cancel;
    // the run, made cold so its list holds the task before it starts; the run itself is _cold's inner task
    private readonly System.Threading.Tasks.Task<System.Threading.Tasks.Task<global::app.data.@this>> _cold;
    private readonly System.Threading.Tasks.Task<global::app.data.@this> _run;
    // set once, by whichever comes first: a Wait (the result is asked for) or the end (nobody asked: a failure is reported)
    private readonly System.Threading.Tasks.TaskCompletionSource _asked = new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The goal it runs.</summary>
    public global::app.goal.@this Goal { get; }

    /// <summary>Which task it is among its actor's — <c>%!app.actor.current.task[id]%</c>.</summary>
    [Out] public global::app.type.item.text.@this Id { get; }

    /// <summary>Whether its run has ended (it has left its actor's list by then).</summary>
    [Out] public global::app.type.item.@bool.@this Ended => new(_run.IsCompleted);

    internal @this(global::app.goal.@this goal, string id, global::app.actor.@this actor,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task<global::app.data.@this>> run,
        System.Action<@this> ended)
    {
        Goal = goal;
        Id = new global::app.type.item.text.@this(id);
        _actor = actor;
        _cancel = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(actor.CancellationToken);
        _cold = new System.Threading.Tasks.Task<System.Threading.Tasks.Task<global::app.data.@this>>(() => Run(run, ended));
        _run = _cold.Unwrap();
    }

    /// <summary>Starts the run — once its list holds it.</summary>
    internal void Begin() => _cold.Start(System.Threading.Tasks.TaskScheduler.Default);

    /// <summary>The run's result, once it ends: the goal's answer, or what it failed with.</summary>
    public async System.Threading.Tasks.Task<global::app.data.@this> Wait()
    {
        _asked.TrySetResult();
        return await _run;
    }

    // Runs the goal call; whatever it throws is its failure. At the end it leaves its list; a failure nobody has asked
    // for goes to the error channel of the actor it failed on.
    private async System.Threading.Tasks.Task<global::app.data.@this> Run(
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task<global::app.data.@this>> run,
        System.Action<@this> ended)
    {
        global::app.data.@this ran;
        try { ran = await run(_cancel.Token); }
        catch (System.Exception ex) when (ex is not (System.OutOfMemoryException or System.StackOverflowException))
        {
            ran = _actor.Context.Error(global::app.error.Error.FromException(ex));
        }
        ended(this);
        _cancel.Dispose();
        if (!ran.Success && _asked.TrySetResult()) await (ran.Context ?? _actor.Context).Actor.Channel.Report(ran);
        return ran;
    }
}
