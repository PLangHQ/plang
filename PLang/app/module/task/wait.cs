namespace app.module.task;

/// <summary>
/// <c>wait for %task%</c> — the task's result, once it ends; <c>wait for %a%, %b%</c> or <c>wait for %task.list%</c>
/// (<see cref="List"/>) — their results, in order. A task that failed fails the step, as any failure does. The wait
/// stops (Cancelled) when the step is cancelled — its timeout, or the task it runs in cancelled; the tasks go on.
/// </summary>
[Action("wait", Cacheable = false)]
public partial class Wait : IContext
{
    /// <summary>The task waited for: its result is the answer.</summary>
    public partial data.@this<global::app.task.@this>? Task { get; init; }

    /// <summary>The tasks waited for: their results, in order, are the answer.</summary>
    public partial data.@this<global::app.type.item.list.@this>? List { get; init; }

    public async System.Threading.Tasks.Task<data.@this> Start()
    {
        // an omitted slot is empty, not null: the slot given is the one that holds something
        if (Task is { IsInitialized: true }) return await Task.Use(task => task.Wait(Context.CancellationToken));
        if (List is not { IsInitialized: true }) return Context.Error(new global::app.error.Error("wait for what? — a task (Task) or tasks (List)", "ValueRequired", 400));
        return await List.Use(async tasks =>
        {
            var results = new List<data.@this>();
            foreach (var row in tasks.Items(Context))
            {
                var result = await row.Use<global::app.task.@this>(task => task.Wait(Context.CancellationToken));
                if (!result.Success) return result;
                results.Add(result);
            }
            return Context.Ok(new global::app.type.item.list.@this(results));
        });
    }
}
