namespace app.callstack;

/// <summary>
/// One captured variable mutation observed during a Call's execution. <c>Before</c> is the
/// pre-mutation value; the current value lives on Variables. For scalar-only capture
/// (default), Before is the raw scalar; for non-scalar values (lists, dicts, objects),
/// Before is a summary string like <c>"&lt;List&lt;int&gt; @ 5042 items&gt;"</c> unless
/// <see cref="Flags.DeepDiff"/> is on, in which case Before is a deep clone.
/// </summary>
public sealed record Diff(string Name, object? Before, DateTimeOffset At);
