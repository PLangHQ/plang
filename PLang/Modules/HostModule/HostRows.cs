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

// Connections now (by state) and the kernel's TCP counters since boot (the caller takes the difference between two reads). ListenQueueMax: the most connections
// waiting for accept() on any listening socket; above 0 for long means the program does not take connections as fast as they come.
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record TcpStats(int Established, int SynRecv, int TimeWait, int CloseWait, int Total, long ListenQueueMax,
	long RetransSegs, long AttemptFails, long EstabResets, long ListenOverflows, long ListenDrops, long Timeouts,
	long InSegs, long OutSegs, long InErrs);

// Pressure stall information: Avg10/60/300 is the percent of time in the last 10, 60 and 300 seconds that tasks waited for the resource (some: at least one task,
// full: all non-idle tasks at once). Total is microseconds waited since boot and only goes up.
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record PressureRow(string Resource, string Kind, double Avg10, double Avg60, double Avg300, long Total);

// CPU time since boot in ticks (100 per second) for all CPUs together. User holds nice and System holds irq and softirq, so User + System + Idle + Iowait + Steal = Total.
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record CpuTicks(long Total, long User, long System, long Idle, long Iowait, long Steal);

// One process. Fds is null when its open files cannot be counted (another user's process, not as root); AgeSeconds is null when /proc/uptime does not read.
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record ProcessStats(int Pid, string Name, long RssMb, int Threads, int? Fds, long CpuSeconds, long? AgeSeconds);

[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record TcpConnection(string Protocol, string State, string LocalAddress, int LocalPort, string RemoteAddress, int RemotePort, long RxQueue, long TxQueue, int Uid);
