namespace app.module.task;

/// <summary>
/// <c>cancel %task%</c> — stops the task: what its goal would do next never happens, and a later <c>wait for</c> fails
/// as Cancelled. The answer is the task's result when it had already ended, nothing (null) when the cancel stopped it.
/// </summary>
[Action("cancel", Cacheable = false)]
public partial class Cancel : IContext
{
    /// <summary>The task to stop.</summary>
    [IsNotNull]
    public partial data.@this<global::app.task.@this> Task { get; init; }

    public System.Threading.Tasks.Task<data.@this> Start() => Task.Use(task => task.Cancel());
}
