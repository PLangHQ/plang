namespace app;

/// <summary>
/// Marker interface. Any type whose presence in a step result means
/// "stop here, capture a Snapshot, return through the channel" implements
/// this. The engine queries via <c>result.ShouldExit()</c>; a Value-side
/// instance can opt out by overriding <see cref="ShouldExit()"/> to false
/// so a "resolved" sentinel can flow through without short-circuiting the
/// step loop. Data with only Type set (no Value) still triggers the
/// Type-side exit check.
/// </summary>
public interface IExitsGoal
{
    /// <summary>
    /// True ⇒ the step loop short-circuits on this Value. Default true.
    /// Override to false when the instance is "resolved" (e.g. an Ask that
    /// already carries the answer).
    /// </summary>
    bool ShouldExit() => true;
}
