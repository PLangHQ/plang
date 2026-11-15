using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PLang.Modules.HostModule;

// In a PLang step the C# names are used (%disk.Mount%, %memory.TotalMb%, %port.Port%); written as JSON (the agent's file, a route's answer) every name is snake case:
// mount, used_percent, total_mb, available_mb, protocol, cpu_seconds.

[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record DiskInfo(string Filesystem, string Mount, long SizeMb, long UsedMb, long AvailableMb, int UsedPercent);

[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record MemoryInfo(long TotalMb, long UsedMb, long FreeMb, long AvailableMb, long SwapTotalMb, long SwapUsedMb);

[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record ListeningPort(string Protocol, string Address, int Port, string? Process, int? Pid);

[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record ProcessInfo(string User, int Pid, string Name, long CpuSeconds);
