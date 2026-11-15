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
