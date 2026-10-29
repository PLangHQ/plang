namespace Bench.meter;

/// <summary>What a stretch of a run cost, from when the meter was made: the bytes this thread allocated (by lap), every
/// thread's, the cpu and the time.</summary>
internal sealed class @this
{
    private long _thread = GC.GetAllocatedBytesForCurrentThread();
    private readonly long _all = GC.GetTotalAllocatedBytes(true);
    private readonly TimeSpan _cpu = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
    private readonly long _start = System.Diagnostics.Stopwatch.GetTimestamp();

    /// <summary>The bytes this thread allocated since the last lap (or since the meter was made).</summary>
    internal long Lap()
    {
        var now = GC.GetAllocatedBytesForCurrentThread();
        var bytes = now - _thread;
        _thread = now;
        return bytes;
    }

    /// <summary>The bytes every thread allocated since the meter was made.</summary>
    internal long All => GC.GetTotalAllocatedBytes(true) - _all;

    /// <summary>The processor time since the meter was made.</summary>
    internal TimeSpan Cpu => System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime - _cpu;

    /// <summary>The time since the meter was made.</summary>
    internal TimeSpan Elapsed => System.Diagnostics.Stopwatch.GetElapsedTime(_start);
}
