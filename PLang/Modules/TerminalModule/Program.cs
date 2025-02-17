

using Microsoft.Extensions.Logging;
using NBitcoin;
using PLang.Attributes;
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

		public async Task RunTerminal(string appExecutableName, List<string>? parameters = null,
			string? pathToWorkingDirInTerminal = null,
			[HandlesVariable] string? dataOutputVariable = null, [HandlesVariable] string? errorDebugInfoOutputVariable = null,
			[HandlesVariable] string? dataStreamDelta = null, [HandlesVariable] string? debugErrorStreamDelta = null,
			bool hideTerminal = false, string? keyValueListSeperator = null
			)
		{
			if (string.IsNullOrWhiteSpace(pathToWorkingDirInTerminal))
			{
				pathToWorkingDirInTerminal = (Goal != null) ? Goal.AbsoluteGoalFolderPath : fileSystem.GoalsPath;
			}
			else
			{
				pathToWorkingDirInTerminal = GetPath(pathToWorkingDirInTerminal);
			}
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
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

					if (parameter.Contains(" ") && !parameter.Contains("\""))
					{
						command += " \"" + parameter + "\"";
					}
					else
					{
						command += " " + parameter;
					}
				}
			}


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
				return;
			}

			var dict = new ReturnDictionary<string, object?>();

			// Start the process
			using (Process process = new Process { StartInfo = startInfo })
			{

				StringBuilder? dataOutput = new();
				StringBuilder? errorOutput = new();




				process.OutputDataReceived += (sender, e) =>
				{
					//logger.LogInformation(e.Data);
					if (string.IsNullOrWhiteSpace(e.Data)) return;

					if (dataStreamDelta != null)
					{
						memoryStack.Put(dataStreamDelta, e.Data);
					}

					dataOutput.Append(e.Data + Environment.NewLine);

					logger.LogTrace(e.Data);
				};


				process.ErrorDataReceived += (sender, e) =>
				{
					if (string.IsNullOrWhiteSpace(e.Data)) return;

					if (!string.IsNullOrEmpty(debugErrorStreamDelta))
					{
						memoryStack.Put(debugErrorStreamDelta, e.Data);
					}
					errorOutput.Append(e.Data + Environment.NewLine);

					logger.LogTrace(e.Data);

				};

				process.Exited += (sender, e) =>
				{
					//logger.LogDebug($"Exited");
				};

				process.Start();
				process.StartInfo.Environment["LANG"] = "en_US.UTF-8";
				process.BeginOutputReadLine(); // Start asynchronous read of output
				process.BeginErrorReadLine(); // Start asynchronous read of error

				// Get the input stream
				StreamWriter sw = process.StandardInput;

				// Write the command to run the application with parameters

				sw.WriteLine(command);

				sw.Close(); // Close the input stream to signal completion
				if (goalStep.WaitForExecution)
				{
					await process.WaitForExitAsync();
				}
				else
				{
					KeepAlive(process, "Process");
				}

				if (!string.IsNullOrEmpty(dataOutputVariable) && dataOutput.Length > 0)
				{
					Dictionary<string, object> keyValuePairs = new Dictionary<string, object>();
					if (keyValueListSeperator != null)
					{

						string[] lines = dataOutput.ToString().Split(['\r', '\n']);
						foreach (var line in lines)
						{
							if (line.Contains(keyValueListSeperator + " ") || line.Contains(keyValueListSeperator + "\t"))
							{
								var data = line.Split(keyValueListSeperator, StringSplitOptions.RemoveEmptyEntries);
								if (data.Length > 1)
								{
									string key = data[0].Trim();
									if (keyValuePairs.ContainsKey(key))
									{
										var objValue = keyValuePairs[key];
										List<object> list = new List<object>(); ;
										if (objValue is IList tmpList)
										{
											foreach (var item in tmpList)
											{
												list.Add(item);
											}
										}
										else
										{
											list.Add(objValue);
										}
										list.Add(string.Join(":", data.Skip(1)).Trim());
										keyValuePairs.AddOrReplace(key, list);
									}
									else
									{
										keyValuePairs.Add(key, string.Join(":", data.Skip(1)).Trim());
									}
								}
							}
						}
						if (keyValuePairs.Count > 0)
						{
							memoryStack.Put(dataOutputVariable, keyValuePairs);
						}
					}

					if (keyValuePairs.Count == 0)
					{

						memoryStack.Put(dataOutputVariable, RemoveLastLine(dataOutput.ToString()));
					}

				}

				if (!string.IsNullOrEmpty(errorDebugInfoOutputVariable))
				{
					memoryStack.Put(errorDebugInfoOutputVariable, errorOutput.ToString());
				}
				else if (errorOutput.Length > 0)
				{
					logger.LogError("No error variable defined so error is written to error log");
					logger.LogError(errorOutput.ToString());
				}

				logger.LogTrace("Done with TerminalModule");
			}

		}

		string? RemoveLastLine(string? input)
		{
			if (input == null) return null;
			input = input.Trim();
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

