namespace app.task.run;

/// <summary>
/// One goal call running on its own — the identity every task item written for it shares: the run, its cancellation,
/// whether its result was asked for. Born only by its actor's list (<see cref="list.@this.Start"/>), cold, so the list
/// holds it before it starts; its cancellation is linked to the actor's, and its own flow cancels by it alone. A
/// failure nobody asked for by its end goes to its actor's error channel; a cancel is asked for, so a cancelled run is
/// never reported.
/// </summary>
internal sealed class @this
{
    private readonly global::app.actor.@this _actor;
    private readonly System.Threading.CancellationTokenSource _cancel;
    private readonly System.Threading.Tasks.Task<System.Threading.Tasks.Task<global::app.data.@this>> _cold;
    private readonly System.Threading.Tasks.Task<global::app.data.@this> _run;
    // set once, by whichever comes first: a Wait or a Cancel (the result is asked for) or the end (nobody asked: a
    // failure is reported)
    private readonly System.Threading.Tasks.TaskCompletionSource _asked = new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

    public global::app.goal.@this Goal { get; }
    public string Id { get; }
    public bool Ended => _run.IsCompleted;

    internal @this(global::app.goal.@this goal, string id, global::app.actor.@this actor,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task<global::app.data.@this>> run,
        System.Action<@this> ended)
    {
        Goal = goal;
        Id = id;
        _actor = actor;
        _cancel = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(actor.CancellationToken);
        _cold = new System.Threading.Tasks.Task<System.Threading.Tasks.Task<global::app.data.@this>>(() => Run(run, ended));
        _run = _cold.Unwrap();
    }

    /// <summary>Starts the run — once its list holds it.</summary>
    internal void Begin() => _cold.Start(System.Threading.Tasks.TaskScheduler.Default);

    /// <summary>The run's result, once it ends: the goal's answer, what it failed with, or Cancelled.</summary>
    public async System.Threading.Tasks.Task<global::app.data.@this> Wait()
    {
        _asked.TrySetResult();
        return await _run;
    }

    /// <summary>Stops the run: ended already, its result; else its cancellation is cancelled and the answer is
    /// nothing (null) — the run ends Cancelled.</summary>
    public async System.Threading.Tasks.Task<global::app.data.@this> Cancel()
    {
        _asked.TrySetResult();
        if (_run.IsCompleted) return await _run;
        try { _cancel.Cancel(); }
        catch (System.ObjectDisposedException) { return await _run; }   // it ended meanwhile
        return _actor.Context.Ok(null);
    }

    // Runs the goal call, handing it this run's token (the context it runs in cancels by it); whatever it throws is its
    // failure, and a run cancelled is Cancelled. At the end it leaves its list; a failure nobody has asked for goes to
    // the error channel of the actor it failed on.
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
        if (_cancel.IsCancellationRequested)
        {
            ran = _actor.Context.Error(new global::app.error.Error($"the task running {Goal.Name} was cancelled", "Cancelled", 499));
            _asked.TrySetResult();
        }
        ended(this);
        _cancel.Dispose();
        if (!ran.Success && _asked.TrySetResult()) await (ran.Context ?? _actor.Context).Actor.Channel.Report(ran);
        return ran;
    }
}
