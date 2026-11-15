namespace PLang.Modules.HostModule;

public record DiskInfo(string Filesystem, string Mount, long SizeMb, long UsedMb, long AvailableMb, int UsedPercent);
public record MemoryInfo(long TotalMb, long UsedMb, long FreeMb, long AvailableMb, long SwapTotalMb, long SwapUsedMb);
public record ListeningPort(string Protocol, string Address, int Port, string? Process, int? Pid);
public record ProcessInfo(string User, int Pid, string Name, long CpuSeconds);
