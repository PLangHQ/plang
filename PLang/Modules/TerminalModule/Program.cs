

using Microsoft.Extensions.Logging;
using PLang.Attributes;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PLang.Modules.TerminalModule
{
    [Description("Terminal/Console access to run external applications")]
	public class Program : BaseProgram
	{
		private readonly ILogger logger;
		private readonly ISettings settings;
		private readonly IOutputStreamFactory outputStreamFactory;
		private readonly IPLangFileSystem fileSystem;
		public static readonly string DefaultOutputVariable = "__Terminal_Output__";
		public static readonly string DefaultErrorOutputVariable = "__Terminal_Error_Output__";
		public Program(ILogger logger, ISettings settings, IOutputStreamFactory outputStreamFactory, IPLangFileSystem fileSystem) : base()
		{
			this.logger = logger;
			this.settings = settings;
			this.outputStreamFactory = outputStreamFactory;
			this.fileSystem = fileSystem;
		}

		public async Task Read(string? variableName)
		{
			var result = outputStreamFactory.CreateHandler().Read();
			memoryStack.Put(variableName, result);
		}

		public async Task<ReturnDictionary<string, object>> RunTerminal(string appExecutableName, List<string>? parameters = null,
			string? pathToWorkingDirInTerminal = null,
			[HandlesVariable] string? dataOutputVariable = null, [HandlesVariable] string? errorDebugInfoOutputVariable = null,
			[HandlesVariable] string? dataStreamDelta = null, [HandlesVariable] string? debugErrorStreamDelta = null
			)
		{
			if (pathToWorkingDirInTerminal == null)
			{
				pathToWorkingDirInTerminal = fileSystem.GoalsPath;
			}
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = false,
				UseShellExecute = false,
				WorkingDirectory = pathToWorkingDirInTerminal
			};

			// Determine the OS and set the appropriate command interpreter
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				startInfo.FileName = "cmd.exe";
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
				return new ReturnDictionary<string, object>();
			}

			var dict = new ReturnDictionary<string, object?>();

			// Start the process
			using (Process process = new Process { StartInfo = startInfo })
			{
				string dataOutput = null;
				string errorOutput = null;
				bool canWriteDataOutputNext = false;
				string command = appExecutableName;
				if (parameters != null)
				{
					foreach (var parameter in parameters) {
						if (parameter == null) continue;

						if (parameter.Contains(" "))
						{
							command += " \"" + parameter + "\"";
						} else
						{
							command += " " + parameter;
						}
					}
				}


				process.OutputDataReceived += (sender, e) =>
				{
					//logger.LogInformation(e.Data);
					if (string.IsNullOrWhiteSpace(e.Data)) return;

					if (!canWriteDataOutputNext && e.Data.ToLower().Contains(appExecutableName))
					{
						canWriteDataOutputNext = true;
						return;
					}

					if (canWriteDataOutputNext)
					{
						if (dataStreamDelta != null)
						{
							memoryStack.Put(dataStreamDelta, e.Data);
						}

						dataOutput += e.Data + Environment.NewLine;
					}
				};


				process.ErrorDataReceived += (sender, e) =>
				{
					if (string.IsNullOrWhiteSpace(e.Data)) return;
					//logger.LogInformation(e.Data);
					if (!string.IsNullOrEmpty(debugErrorStreamDelta))
					{
						memoryStack.Put(debugErrorStreamDelta, e.Data);
					} else
					{
						if (string.IsNullOrWhiteSpace(errorOutput))
						{
							logger.LogError("Command: " + command);
						}
						logger.LogError(e.Data);
					}
					errorOutput += e.Data + Environment.NewLine;
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
				
				sw.WriteLine(command);

				sw.Close(); // Close the input stream to signal completion
				await process.WaitForExitAsync();

				if (!string.IsNullOrEmpty(dataOutputVariable))
				{
					memoryStack.Put(dataOutputVariable, RemoveLastLine(dataOutput?.Trim()));
					dict.Add(dataOutputVariable, RemoveLastLine(dataOutput?.Trim()));
				}

				if (!string.IsNullOrEmpty(errorDebugInfoOutputVariable))
				{
					memoryStack.Put(errorDebugInfoOutputVariable, errorOutput);
					dict.Add(errorDebugInfoOutputVariable, errorOutput);
				}
			}

			return dict;
		}

		string RemoveLastLine(string input)
		{
			if (input == null) return null;

			// Split the string using Environment.NewLine
			string[] lines = input.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

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

