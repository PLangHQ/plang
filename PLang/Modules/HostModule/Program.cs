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
}
