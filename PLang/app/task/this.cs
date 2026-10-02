namespace app.task;

/// <summary>
/// A goal call running on its own — what <c>call X in parallel</c> answers: its run, and the tasks it replaced. Written
/// where a task was, it keeps that one (<see cref="Replace"/>), so <see cref="list"/> is every task written to the
/// variable, in the order written, the last itself. Its result is reached through <see cref="Wait"/>, one door; a
/// failure nobody asked for by the time it ends goes to its actor's error channel, and a later Wait still answers it.
/// </summary>
[global::app.Attributes.PlangType("task")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    private readonly run.@this _run;
    private readonly @this? _replaced;

    internal @this(run.@this run, @this? replaced = null)
    {
        _run = run;
        _replaced = replaced;
    }

    /// <summary>The goal it runs.</summary>
    public global::app.goal.@this Goal => _run.Goal;

    /// <summary>Which task it is among its actor's — <c>%!app.actor.current.task[id]%</c>.</summary>
    [Out] public global::app.type.item.text.@this Id => new(_run.Id);

    /// <summary>Whether its run has ended (it has left its actor's list by then).</summary>
    [Out] public global::app.type.item.@bool.@this Ended => new(_run.Ended);

    /// <summary>Every task written where this one is, in the order written — the last this one.</summary>
    public global::app.type.item.list.@this list => new(Written.Cast<global::app.type.item.@this>());

    private System.Collections.Generic.IEnumerable<@this> Written
        => (_replaced?.Written ?? []).Append(this);

    /// <summary>Its run's result, once it ends: the goal's answer, what it failed with, or Cancelled; the wait stops
    /// when <paramref name="token"/> (its waiter's) is cancelled.</summary>
    public System.Threading.Tasks.Task<global::app.data.@this> Wait(System.Threading.CancellationToken token = default)
        => _run.Wait(token);

    /// <summary>Stops its run: ended already, its result; else nothing (null), and the run ends Cancelled.</summary>
    public System.Threading.Tasks.Task<global::app.data.@this> Cancel() => _run.Cancel();

    /// <summary>Written where another run's task was: this run, keeping that task — <see cref="list"/> holds both. Over
    /// anything else (its own run's task included) it is itself.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Replace(
        System.Func<System.Threading.Tasks.ValueTask<global::app.type.item.@this?>> previous)
        => await previous() is @this before && !ReferenceEquals(before._run, _run) ? new @this(_run, before) : this;
}
