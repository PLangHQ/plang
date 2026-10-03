using System.Runtime.InteropServices;

namespace app.module.terminal.code;

/// <summary>
/// The programs this plang started: they end with it. On Windows a program outlives the one that
/// started it — a plang that ends without stopping them (its window closed, Ctrl+C, a crash) would
/// leave them running (PlangOS's wsl.exe kept a whole PlangOS session alive, and WSL wedged when
/// the distro was removed). So each program goes into a Windows job that closes when this plang's
/// last handle to it does, which ends every program in it. Elsewhere: nothing to do (a program there
/// ends when its input closes, as PlangOS does).
/// </summary>
internal static class Children
{
    private static readonly Lazy<IntPtr> Job = new(Create);

    /// <summary><paramref name="process"/> ends when this plang does (a Windows program .NET started; a spawned one, on
    /// Linux, ends with its input).</summary>
    internal static void Adopt(child.@this process)
    {
        if (!OperatingSystem.IsWindows() || Job.Value == IntPtr.Zero || process is not child.managed.@this managed) return;
        try { AssignProcessToJobObject(Job.Value, managed.Process.Handle); }
        catch (InvalidOperationException) { /* it has already ended */ }
    }

    private static IntPtr Create()
    {
        var job = CreateJobObjectW(IntPtr.Zero, null);
        if (job == IntPtr.Zero) return IntPtr.Zero;
        var limits = new ExtendedLimits { Basic = new BasicLimits { Flags = KillOnJobClose } };
        var size = Marshal.SizeOf<ExtendedLimits>();
        var memory = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(limits, memory, false);
            if (!SetInformationJobObject(job, ExtendedLimitInformation, memory, (uint)size)) return IntPtr.Zero;
        }
        finally { Marshal.FreeHGlobal(memory); }
        return job;   // held for the process's life: the job closes when plang does
    }

    private const int ExtendedLimitInformation = 9;
    private const uint KillOnJobClose = 0x2000;

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimits
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint Flags;
        public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass, SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Counters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimits
    {
        public BasicLimits Basic;
        public Counters Io;
        public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObjectW(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
}
