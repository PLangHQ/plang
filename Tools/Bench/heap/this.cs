namespace Bench.heap;

/// <summary>What the process holds.</summary>
internal static class @this
{
    /// <summary>MB held after everything let go is collected.</summary>
    internal static double Held()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        return GC.GetTotalMemory(false) / 1024.0 / 1024.0;
    }
}
