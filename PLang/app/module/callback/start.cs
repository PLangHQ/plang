namespace app.module.callback;

/// <summary>
/// PLang's <c>- start %callback%</c> verb: goes on with the run a callback holds (an error's <c>%!error.callback%</c>)
/// through the one resume door, <c>snapshot.resume</c>, so a callback is checked as every resume is: unsigned, changed
/// since it was signed, or another app's, it is refused before anything is restored.
/// </summary>
[Action("start", Cacheable = false)]
public partial class start : IContext
{
    /// <summary>The callback to start: the signed snapshot a callback holds.</summary>
    [IsNotNull]
    public partial data.@this Callback { get; init; }

    public Task<data.@this> Start()
        => new global::app.goal.step.action.composed.@this(new global::app.module.snapshot.resume(Context)
        {
            Snapshot = global::app.data.@this<global::app.snapshot.@this>.From(Callback),
        }, Context).Start(Context);
}
