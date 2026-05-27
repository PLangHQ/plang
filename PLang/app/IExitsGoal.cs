namespace app;

/// <summary>
/// Marker interface. Any type whose presence in a step result means
/// "stop here, capture a Snapshot, return through the channel" implements
/// this. The engine queries via <c>result.ShouldExit()</c> — a Value-side
/// instance can opt out via <see cref="ShouldExit()"/> (returning false) so
/// a "resolved" sentinel can flow through without short-circuiting the step
/// loop. Data with only Type set (no Value) keeps falling through to the
/// Type-side check unchanged.
///
/// Implementers in this branch: <see cref="app.modules.output.Ask"/> (returns
/// false once Answer is bound on the resume path).
/// </summary>
public interface IExitsGoal
{
    /// <summary>
    /// True ⇒ the step loop short-circuits on this Value. Default true (the
    /// historical IExitsGoal semantics). Override to false when the instance
    /// has been "resolved" — e.g. an Ask that already carries the answer.
    /// </summary>
    bool ShouldExit() => true;
}
