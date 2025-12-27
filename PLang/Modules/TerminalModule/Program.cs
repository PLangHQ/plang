

using Microsoft.Extensions.Logging;
using NBitcoin;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace PLang.Modules.TerminalModule
{
	[Description("Terminal/Console access to run external applications")]
	public class Program : BaseProgram
	{
		private readonly ILogger logger;
		private readonly ISettings settings;
		private readonly IPLangFileSystem fileSystem;
		private readonly ProgramFactory programFactory;
		private readonly IEngine engine;
		public static readonly string DefaultOutputVariable = "__Terminal_Output__";
		public static readonly string DefaultErrorOutputVariable = "__Terminal_Error_Output__";
		public Program(ILogger logger, ISettings settings, IPLangFileSystem fileSystem, ProgramFactory programFactory, IEngine engine) : base()
		{
			this.logger = logger;
			this.settings = settings;
			this.fileSystem = fileSystem;
			this.programFactory = programFactory;
			this.engine = engine;
		}

		public async Task Read(string? variableName)
		{
			throw new NotImplementedException("Read is not implemented");
		}

		[Description("Run a executable. Parameters string should not be escaped. Return instance of the process")]
		[Example("run command myapp.exe, write to %result%", "appExecutableName=myapp.exe, return values=%result%")]
		[Example("run git --status, write to %output%", @"appExecutableName=git, parameters=""--status"", variableNameForDeltaOnStandardStream=null, variableNameForDeltaOnErrorStream=null, ReturnValues = %output%")]
		[Example("terminal ffmpeg -i input.mp4 output.avi, %delta%, %errorDelta%, write to %data%", @"appExecutableName=ffmpeg, parameters=""-i"",""input.mp4"",""output.avi"", variableNameForDeltaOnStandardStream=%delta%, variableNameForDeltaOnErrorStream=%errorDelta%, ReturnValues should be %data%")]
		public async Task<(object?, IError?, Properties?)> RunTerminal(string appExecutableName, List<string>? parameters = null,
			string? pathToWorkingDirInTerminal = null,
			GoalToCallInfo? onStandardOutput = null, GoalToCallInfo? onErrorOutput = null, GoalToCallInfo? onExit = null,
			bool hideTerminal = false
			)
		{
			if (string.IsNullOrWhiteSpace(pathToWorkingDirInTerminal))
			{
				if (Goal != null && Goal.IsSystem && context.CallingStep != null)
				{
					pathToWorkingDirInTerminal = context.CallingStep.Goal.AbsoluteGoalFolderPath;
				}
				else
				{
					pathToWorkingDirInTerminal = (Goal != null) ? Goal.AbsoluteGoalFolderPath : fileSystem.GoalsPath;
				}
			}
			else
			{
				pathToWorkingDirInTerminal = GetPath(pathToWorkingDirInTerminal);
			}

			var fileNameWithPath = GetPath(appExecutableName);

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && string.IsNullOrEmpty(Path.GetExtension(appExecutableName)))
			{
				appExecutableName = appExecutableName + ".exe";
			}

			if (!fileSystem.File.Exists(fileNameWithPath))
			{
				fileNameWithPath = GetPath(Path.Join("/bin", appExecutableName));
				if (!fileSystem.File.Exists(fileNameWithPath))
				{
					// executeable file not found so it must be in PATH, use just name
					fileNameWithPath = appExecutableName;
				}
			}

			Properties properties = new();
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = fileNameWithPath,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = hideTerminal,
				UseShellExecute = false,
				WorkingDirectory = pathToWorkingDirInTerminal,

			};


			string command = appExecutableName;
			if (parameters != null)
			{
				foreach (var parameter in parameters)
				{
					if (parameter == null) continue;

					startInfo.ArgumentList.Add(parameter);
				}
			}

			startInfo.StandardInputEncoding = Encoding.UTF8;
			startInfo.StandardOutputEncoding = Encoding.UTF8;
			startInfo.StandardErrorEncoding = Encoding.UTF8;

			Console.OutputEncoding = Encoding.UTF8;
			Console.InputEncoding = Encoding.UTF8;

			// Start the process
			Process process = new Process { StartInfo = startInfo };
			goal.AddVariable(process, () =>
			{
				process.Dispose();
				return Task.CompletedTask;
			});

			properties.Add(new ObjectValue("Process", process));
			properties.Add(new ObjectValue("StartInfo", startInfo));

			process.OutputDataReceived += async (sender, e) =>
			{
				//logger.LogInformation(e.Data);
				if (onStandardOutput == null || string.IsNullOrWhiteSpace(e.Data)) return;

				var goalToCall = onStandardOutput.Clone();
				goalToCall.Parameters.AddOrReplace("sender", sender);
				goalToCall.Parameters.AddOrReplace("data", e.Data);
				goalToCall.Parameters.AddOrReplace("event", e);

				await engine.RunGoal(goalToCall, goal, context);


				logger.LogTrace(e.Data);
			};


			process.ErrorDataReceived += async (sender, e) =>
			{
				if (onErrorOutput == null || string.IsNullOrWhiteSpace(e.Data)) return;

				var goalToCall = onErrorOutput.Clone();
				goalToCall.Parameters.Add("sender", sender);
				goalToCall.Parameters.Add("data", e.Data);
				goalToCall.Parameters.Add("event", e);

				await engine.RunGoal(goalToCall, goal, context);

				logger.LogTrace(e.Data);

			};

			process.Exited += async (sender, e) =>
			{
				if (onExit == null) return;

				onExit.Parameters.Add("sender", sender);
				onExit.Parameters.Add("event", e);

				await engine.RunGoal(onExit, goal, context);

			};

			process.Start();

			process.BeginOutputReadLine(); // Start asynchronous read of output
			process.BeginErrorReadLine(); // Start asynchronous read of error

			// Get the input stream
			StreamWriter sw = process.StandardInput;

			// Write the command to run the application with parameters

			//sw.WriteLine(command);
			
			// Close the input stream to signal completion

			object? returnProcess = null;
			if (goalStep.WaitForExecution)
			{
				sw.Close();
				await process.WaitForExitAsync();
			}
			else
			{
				returnProcess = process;
				KeepAlive(process, "Process");
			}

			logger.LogTrace("Done with TerminalModule");

			return (returnProcess, null, properties);
		}

		public async Task WaitForProcessToExit(Process process)
		{
			await process.WaitForExitAsync();
		}

		public async Task Kill(Process process, bool killEntireProcessTree = true)
		{
			process.Kill(killEntireProcessTree);
		}

	}


}

