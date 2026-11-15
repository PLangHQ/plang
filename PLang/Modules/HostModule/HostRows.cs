using Newtonsoft.Json;

namespace PLang.Modules.HostModule;

// The JSON names are the agent's own, snake case, and where the agent has always written a key (capacity, mount, total_mb, avail_mb, proto, local)
// the same one: the watcher's rules and page read those, and a server on the old agent and one on the new look the same to them.
// In a PLang step the C# names are used: %disk.Mount%, %memory.TotalMb%, %port.Port%.

public record DiskInfo(
	[property: JsonProperty("filesystem")] string Filesystem,
	[property: JsonProperty("mount")] string Mount,
	[property: JsonProperty("size_mb")] long SizeMb,
	[property: JsonProperty("used_mb")] long UsedMb,
	[property: JsonProperty("available_mb")] long AvailableMb,
	[property: JsonProperty("capacity")] int UsedPercent);

public record MemoryInfo(
	[property: JsonProperty("total_mb")] long TotalMb,
	[property: JsonProperty("used_mb")] long UsedMb,
	[property: JsonProperty("free_mb")] long FreeMb,
	[property: JsonProperty("avail_mb")] long AvailableMb,
	[property: JsonProperty("swap_total_mb")] long SwapTotalMb,
	[property: JsonProperty("swap_used_mb")] long SwapUsedMb);

public record ListeningPort(
	[property: JsonProperty("proto")] string Protocol,
	[property: JsonProperty("address")] string Address,
	[property: JsonProperty("port")] int Port,
	[property: JsonProperty("process")] string? Process,
	[property: JsonProperty("pid")] int? Pid)
{
	/// <summary>"address:port", an IPv6 address in brackets: [::]:22. This is the text the baseline of open ports is made of.</summary>
	[JsonProperty("local")]
	public string Local => (Address.Contains(':') ? "[" + Address + "]" : Address) + ":" + Port;
}

public record ProcessInfo(
	[property: JsonProperty("user")] string User,
	[property: JsonProperty("pid")] int Pid,
	[property: JsonProperty("name")] string Name,
	[property: JsonProperty("cpu_seconds")] long CpuSeconds);
