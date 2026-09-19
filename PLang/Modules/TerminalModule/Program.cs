

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

		[Description("Run an executable. Parameter strings should not be escaped.")]
		[Example("terminal git --status, write to %output%", @"appExecutableName=git, parameters=""--status"", ReturnValues = %output%")]
		[Example("terminal ffmpeg -i input.mp4 output.avi, %delta%, %errorDelta%, write to %data%", @"appExecutableName=ffmpeg, parameters=""-i"",""input.mp4"",""output.avi"", variableNameForDeltaOnStandardStream=%delta%, variableNameForDeltaOnErrorStream=%errorDelta%, ReturnValues should be %data%")]
		public async Task<(object?, IError?, Properties?)> RunTerminal(
			[Description("The program to run, e.g. git in `terminal git --status`, or a full path such as /usr/bin/ffmpeg")] string appExecutableName,
			[Description("The arguments handed to the program, one entry each, in the order the step writes them")] List<string>? parameters = null,
			[Description("The directory the program runs in, stated by the step as e.g. `working dir: \"/srv/app\"`")] string? pathToWorkingDirInTerminal = null,
			// A step saying `write to %output%` was coming back with %output% here instead of as a
			// return value, which streams the output away line by line and returns nothing.
			[Description("Almost always unset. Only when the step names a variable to receive standard output line by line as it arrives, e.g. `%delta%`. `write to %x%` is the finished output and belongs in ReturnValues, never here")] [HandlesVariable] string? variableNameForDeltaOnStandardStream = null,
			[Description("Almost always unset. Only when the step names a separate variable for the error stream, line by line as it arrives, e.g. `%errorDelta%`")] [HandlesVariable] string? variableNameForDeltaOnErrorStream = null,
			[Description("true when the step says the terminal window should not be shown, e.g. `hide terminal`. false otherwise")] bool hideTerminal = false
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
			/*
			// Determine the OS and set the appropriate command interpreter
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				if (command.Contains(".ps") || command.Contains("|") || command.Contains(">"))
				{
					startInfo.FileName = "powershell.exe";
				}
				else
				{
					startInfo.FileName = "cmd.exe";
				}
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				startInfo.FileName = "/bin/bash";
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				startInfo.FileName = "/bin/zsh";
			}
			else
			{
				logger.LogError("Unsupported OS");
				return (null, new ProgramError("Unsupported OS", goalStep, function), properties);
			}*/

			// Start the process
			Process process = new Process { StartInfo = startInfo };
			context.AddVariable(process, () =>
			{
				process.Dispose();
				return Task.CompletedTask;
			});

			properties.Add(new ObjectValue("Process", process));
			properties.Add(new ObjectValue("StartInfo", startInfo));

			StringBuilder? dataOutput = new();
			StringBuilder? errorOutput = new();

			process.OutputDataReceived += (sender, e) =>
			{
				//logger.LogInformation(e.Data);
				if (string.IsNullOrWhiteSpace(e.Data)) return;

				if (!string.IsNullOrEmpty(variableNameForDeltaOnStandardStream))
				{
					memoryStack.Put(variableNameForDeltaOnStandardStream, e.Data, goalStep: goalStep);
				}

				dataOutput.Append(e.Data + Environment.NewLine);

				logger.LogTrace(e.Data);
			};


			process.ErrorDataReceived += (sender, e) =>
			{
				if (string.IsNullOrWhiteSpace(e.Data)) return;

				if (!string.IsNullOrEmpty(variableNameForDeltaOnErrorStream))
				{
					memoryStack.Put(variableNameForDeltaOnErrorStream, e.Data, goalStep: goalStep);
				}
				errorOutput.Append(e.Data + Environment.NewLine);

				logger.LogTrace(e.Data);

			};

			process.Exited += (sender, e) =>
			{
				//logger.LogDebug($"Exited");
			};

			process.Start();

			process.BeginOutputReadLine(); // Start asynchronous read of output
			process.BeginErrorReadLine(); // Start asynchronous read of error

			// Get the input stream
			StreamWriter sw = process.StandardInput;

			// Write the command to run the application with parameters

			//sw.WriteLine(command);
			sw.Close();
			// Close the input stream to signal completion
			if (goalStep.WaitForExecution)
			{
				await process.WaitForExitAsync();
				
			}
			else
			{
				KeepAlive(process, "Process");
			}

			IError? error = null;
			if (errorOutput.Length > 0)
			{
				command = appExecutableName = string.Join(" ", startInfo.ArgumentList);
				error = new ProgramError($"Command: {command}\n{errorOutput}", goalStep, function);
			}
			logger.LogTrace("Done with TerminalModule");

			return (dataOutput.ToString(), error, properties);
		}


	}
}

