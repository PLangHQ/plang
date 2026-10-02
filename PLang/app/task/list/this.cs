using System.Collections.Concurrent;

namespace app.task.list;

/// <summary>
/// An actor's running tasks — <c>%!app.actor.current.task%</c>, as <c>channel</c> is its channels. It makes each task
/// (<see cref="Start"/>), holds it while it runs and lets it go when it ends: the whole life of a task is here. A task
/// started inside another task's goal lists under the same actor.
/// </summary>
public sealed class @this : global::app.type.item.@this
{
    private readonly ConcurrentDictionary<string, task.@this> _running = new(StringComparer.Ordinal);
    private readonly global::app.actor.@this _actor;

    internal @this(global::app.actor.@this actor) => _actor = actor;

    /// <summary>The tasks running now.</summary>
    public IEnumerable<task.@this> list => _running.Values;

    /// <summary>A task running <paramref name="run"/> for <paramref name="goal"/>: listed, then started; it leaves the
    /// list when it ends. Its cancellation is linked to the actor's.</summary>
    public task.@this Start(global::app.goal.@this goal,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task<global::app.data.@this>> run)
    {
        var id = System.Guid.NewGuid().ToString("N");
        var started = new task.@this(goal, id, _actor, run, ended => _running.TryRemove(id, out _));
        _running[id] = started;
        started.Begin();
        return started;
    }

    /// <summary>One step down: the list's own members first, then a running task by its id.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
    {
        var member = await base.Get(parent, key);
        if (member.IsInitialized) return member;
        return _running.TryGetValue(key, out var running) ? new global::app.data.@this(key, running, parent: parent) : member;
    }
}
