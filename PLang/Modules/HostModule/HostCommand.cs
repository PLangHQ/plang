using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Building.Model;
using PLang.Modules.TerminalModule;
using System.Runtime.InteropServices;

namespace PLang.Modules.HostModule;

/// <summary>
/// Runs one fixed command through the terminal module and gives back its text. The module that asks (host or environment) passes only
/// literal arguments: nothing a user wrote ever becomes part of the command line. LC_ALL=C makes the output the same on every machine, whatever its language.
/// </summary>
public static class HostCommand
{
	public static async Task<(string?, IError?)> Run(BaseProgram caller, GoalStep step, string program, params string[] arguments)
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			return (null, new ProgramError($"{program} is read from a Linux machine for now; this one is {RuntimeInformation.OSDescription}", step, Key: "NotSupportedOnThisOs"));
		}

		var terminal = caller.GetProgramModule<TerminalModule.Program>();
		var args = new List<string> { "LC_ALL=C", program };
		args.AddRange(arguments);

		var (output, error, _) = await terminal.RunTerminal("env", args, hideTerminal: true);
		if (error != null) return (null, error);
		return (output?.ToString() ?? "", null);
	}
}
