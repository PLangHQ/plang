

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
		private readonly IOutputStreamFactory outputStreamFactory;
		private readonly IPLangFileSystem fileSystem;
		private readonly ProgramFactory programFactory;
		private readonly IEngine engine;
		public static readonly string DefaultOutputVariable = "__Terminal_Output__";
		public static readonly string DefaultErrorOutputVariable = "__Terminal_Error_Output__";
		public Program(ILogger logger, ISettings settings, IOutputStreamFactory outputStreamFactory, IPLangFileSystem fileSystem, ProgramFactory programFactory, IEngine engine) : base()
		{
			this.logger = logger;
			this.settings = settings;
			this.outputStreamFactory = outputStreamFactory;
			this.fileSystem = fileSystem;
			this.programFactory = programFactory;
			this.engine = engine;
		}

		public async Task Read(string? variableName)
		{
			var result = outputStreamFactory.CreateHandler().Read();
			memoryStack.Put(variableName, result);
		}

		[Description("Run a executable. Parameters string should not be escaped.")]
		public async Task<(object?, IError?, IProperties?)> RunTerminal(string appExecutableName, List<string>? parameters = null,
			string? pathToWorkingDirInTerminal = null,
			GoalToCall? onDataCallGoal = null, GoalToCall? onErrorCallGoal = null,
			bool hideTerminal = false
			)
		{
			if (string.IsNullOrWhiteSpace(pathToWorkingDirInTerminal))
			{
				if (Goal != null && Goal.IsOS && engine.CallingStep != null)
				{
					pathToWorkingDirInTerminal = engine.CallingStep.Goal.AbsoluteGoalFolderPath;
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
			using Process process = new Process { StartInfo = startInfo };

			properties.Add("Process", process);
			properties.Add("StartInfo", startInfo);

			StringBuilder? dataOutput = new();
			StringBuilder? errorOutput = new();

			CallGoalModule.Program? caller = null;
			if (!string.IsNullOrEmpty(onDataCallGoal) || !string.IsNullOrEmpty(onErrorCallGoal))
			{
				caller = programFactory.GetProgram<CallGoalModule.Program>();
			}

			lineCounter = 0;
			process.OutputDataReceived += async (sender, e) =>
			{
				//logger.LogInformation(e.Data);
				if (string.IsNullOrWhiteSpace(e.Data)) return;

				if (!string.IsNullOrEmpty(onDataCallGoal) && caller != null)
				{
					var dict = new Dictionary<string, object?>();
					dict.Add("data", e.Data);
					await caller.RunGoal(onDataCallGoal, dict);
				}

				dataOutput.Append(e.Data + Environment.NewLine);

				logger.LogTrace(e.Data);
			};


			process.ErrorDataReceived += async (sender, e) =>
			{
				if (string.IsNullOrWhiteSpace(e.Data)) return;

				if (!string.IsNullOrEmpty(onErrorCallGoal) && caller != null)
				{
					var dict = new Dictionary<string, object?>();
					dict.Add("error", e.Data);
					await caller.RunGoal(onErrorCallGoal, dict);
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

			sw.Close(); // Close the input stream to signal completion
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

			return (dataOutput, error, properties);
		}

		int lineCounter = 0;
		private bool IsFirst3Lines()
		{
			return (++lineCounter <= 3);
		}

		string? RemoveLastLine(StringBuilder? input)
		{
			// Only remove the last line on Windows, not sure how it is on Linux and Macos
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return input.ToString();

			if (input == null) return null;
			//input = input.Trim();
			// Split the string using Environment.NewLine
			string[] lines = input.ToString().TrimEnd().Split(new[] { Environment.NewLine }, StringSplitOptions.None);

			// If there's only one line or no lines, return an empty string
			if (lines.Length <= 1)
			{
				return string.Empty;
			}

			// Join the array back into a single string, excluding the last element
			return string.Join(Environment.NewLine, lines, 0, lines.Length - 1);
		}

	}
}

