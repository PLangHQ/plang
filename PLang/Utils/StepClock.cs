using System.Diagnostics;

namespace PLang.Utils;

/// <summary>
/// Elapsed time for log lines without a Stopwatch object per step: take <see cref="Now"/> at the start and
/// call <see cref="Ms"/> inside the log call (which is only evaluated when that log level is on).
/// </summary>
public static class StepClock
{
	public static long Now() => Stopwatch.GetTimestamp();
	public static long Ms(long startTimestamp) => (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
}
