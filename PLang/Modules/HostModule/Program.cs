using PLang.Attributes;
using PLang.Errors;
using System.ComponentModel;

namespace PLang.Modules.HostModule;

[Description("Look at what runs on the machine, as a sysadmin does: which ports listen and which program owns them, which processes run and as whom. Read only. Linux")]
[Effect(Effects.Opaque)]
public class Program : BaseProgram
{
	// Each function runs one fixed command through the terminal module and turns its text into typed rows (HostParsers). Nothing the step says
	// is ever part of the command line. Later a function may read /proc itself instead; the step and its result stay the same.

	[Description("The ports this machine listens on (tcp and udp), with the program that owns each when the app may see it (root sees all). Each row: Protocol, Address, Port, Process, Pid. e.g. `get listening ports, write to %ports%`")]
	[Example("get listening ports, write to %ports%", "")]
	[Example("which ports does this machine listen on, write to %open_ports%", "")]
	public async Task<(List<ListeningPort>?, IError?)> GetListeningPorts()
	{
		var (text, error) = await HostCommand.Run(this, goalStep, "ss", "-tulnpH");
		if (error != null) return (null, error);
		return (HostParsers.ParseSs(text!), null);
	}

	[Description("The running processes: User, Pid, Name, CpuSeconds (CPU time used so far). e.g. `get processes, write to %processes%`")]
	[Example("get processes, write to %processes%", "")]
	[Example("list the running processes, write to %procs%", "")]
	public async Task<(List<ProcessInfo>?, IError?)> GetProcesses()
	{
		var (text, error) = await HostCommand.Run(this, goalStep, "ps", "-eo", "user:32,pid,comm:32,times", "--no-headers");
		if (error != null) return (null, error);
		return (HostParsers.ParsePs(text!), null);
	}
}
