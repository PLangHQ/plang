using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PLang.Modules.HostModule;

[Description("Look at what runs on the machine, as a sysadmin does: which ports listen and which program owns them, which processes run and as whom. Read only. Linux")]
[Effect(Effects.None)]
public class Program : BaseProgram
{
	// Read from /proc (ProcReader): no program is started, so nothing that has replaced ss or ps can answer for the kernel.

	private IError? OnlyLinux() =>
		RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? null
			: new ProgramError($"This reads /proc, a Linux machine's. This one is {RuntimeInformation.OSDescription}", goalStep, Key: "NotSupportedOnThisOs");

	[Description("The ports this machine listens on (tcp and udp), with the program that owns each when it can be seen (root sees all, another user only its own). Each row: Protocol, Address, Port, Process, Pid. e.g. `get listening ports, write to %ports%`")]
	[Example("get listening ports, write to %ports%", "")]
	[Example("which ports does this machine listen on, write to %open_ports%", "")]
	public async Task<(List<ListeningPort>?, IError?)> GetListeningPorts()
	{
		var error = OnlyLinux();
		if (error != null) return (null, error);
		return (ProcReader.ReadListeningPorts(), null);
	}

	[Description("The TCP connections now counted by state, and the kernel's TCP counters since boot. One object: Established, SynRecv, TimeWait, CloseWait, Total, ListenQueueMax (the most connections waiting for accept on a listening socket), RetransSegs, AttemptFails, EstabResets, ListenOverflows, ListenDrops, Timeouts. The counters only go up: take the difference between two reads. e.g. `get tcp stats, write to %tcp%`")]
	[Example("get tcp stats, write to %tcp%", "")]
	[Example("how many tcp connections are open, write to %tcp%", "")]
	public async Task<(TcpStats?, IError?)> GetTcpStats()
	{
		var error = OnlyLinux();
		if (error != null) return (null, error);
		return (ProcReader.ReadTcpStats(), null);
	}

	[Description("Every TCP connection and listening socket as a list, the long form of `get tcp stats`. Each row: Protocol, State (established, time_wait, close_wait, listen ...), LocalAddress, LocalPort, RemoteAddress, RemotePort, RxQueue, TxQueue, Uid. A busy server has thousands of rows: use `get tcp stats` when the numbers are enough. e.g. `get tcp stats full, write to %connections%`")]
	[Example("get tcp stats full, write to %connections%", "")]
	[Example("list every tcp connection, write to %connections%", "")]
	public async Task<(List<TcpConnection>?, IError?)> GetTcpConnections()
	{
		var error = OnlyLinux();
		if (error != null) return (null, error);
		return (ProcReader.ReadTcpConnections(), null);
	}

	[Description("The running processes: User, Pid, Name, CpuSeconds (CPU time used so far). e.g. `get processes, write to %processes%`")]
	[Example("get processes, write to %processes%", "")]
	[Example("list the running processes, write to %procs%", "")]
	public async Task<(List<ProcessInfo>?, IError?)> GetProcesses()
	{
		var error = OnlyLinux();
		if (error != null) return (null, error);
		return (ProcReader.ReadProcesses(), null);
	}

	[Description("How much CPU, memory and io held tasks back (pressure stall information, /proc/pressure). Each row: Resource (cpu, memory, io), Kind (some: at least one task waited, full: all non-idle tasks waited at once), Avg10, Avg60, Avg300 (percent of the last 10, 60, 300 seconds), Total (microseconds waited since boot, only goes up). cpu has no full row on older kernels; a kernel without pressure information gives an empty list. e.g. `get pressure, write to %pressure%`")]
	[Example("get pressure, write to %pressure%", "")]
	[Example("how much is the machine waiting for cpu, memory and io, write to %psi%", "")]
	public async Task<(List<PressureRow>?, IError?)> GetPressure()
	{
		var error = OnlyLinux();
		if (error != null) return (null, error);
		return (ProcReader.ReadPressure(), null);
	}

	[Description("CPU time since boot in ticks (100 per second), all CPUs together, from /proc/stat. One object: Total, User (includes nice), System (includes irq and softirq), Idle, Iowait, Steal; User + System + Idle + Iowait + Steal = Total. The ticks only go up: take the difference between two reads, busy percent = 100 * (Δtotal - Δidle - Δiowait) / Δtotal. e.g. `get cpu ticks, write to %cpu%`")]
	[Example("get cpu ticks, write to %cpu%", "")]
	[Example("read the cpu time counters, write to %ticks%", "")]
	public async Task<(CpuTicks?, IError?)> GetCpuTicks()
	{
		var error = OnlyLinux();
		if (error != null) return (null, error);
		return (ProcReader.ReadCpuTicks(), null);
	}

	[Description("One process by its pid. One object: Pid, Name, RssMb (memory it holds), Threads, Fds (open files; null when they cannot be counted, which needs root for another user's process), CpuSeconds (CPU time used so far), AgeSeconds (seconds since it started). A pid that does not exist gives null, not an error. Takes a pid only; for a systemd service get its pid first, e.g. `run systemctl show -p MainPID --value plang`. e.g. `get process stats for pid %pid%, write to %p%`")]
	[Example("get process stats for pid %pid%, write to %p%", "pid=%pid%")]
	[Example("get stats of process 1234, write to %process%", "pid=1234")]
	public async Task<(ProcessStats?, IError?)> GetProcessStats(int pid)
	{
		var error = OnlyLinux();
		if (error != null) return (null, error);
		return (ProcReader.ReadProcessStats(pid), null);
	}
}
