

using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json.Linq;
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
	[Effect(Effects.Opaque)]
	public class Program : BaseProgram
	{
		private readonly ILogger logger;
		private readonly ISettings settings;
		private readonly IPLangFileSystem fileSystem;
		private readonly ProgramFactory programFactory;
		private readonly IEngine engine;
		private readonly IPseudoRuntime pseudoRuntime;
		public static readonly string DefaultOutputVariable = "__Terminal_Output__";
		public static readonly string DefaultErrorOutputVariable = "__Terminal_Error_Output__";
		public const string ProcessKeepAliveKind = "Process";
		public Program(ILogger logger, ISettings settings, IPLangFileSystem fileSystem, ProgramFactory programFactory, IEngine engine, IPseudoRuntime pseudoRuntime) : base()
		{
			this.logger = logger;
			this.settings = settings;
			this.fileSystem = fileSystem;
			this.programFactory = programFactory;
			this.engine = engine;
			this.pseudoRuntime = pseudoRuntime;
		}

		[Description("Start a program that keeps running beside the app, and return it, so the step writes it to a %variable%. Each line the program writes is handed to onOutputLine (standard output) or onErrorLine (standard error) as %line%, one at a time and in order, with the process as %process%; onExit gets %exitCode% when it ends. Write to its standard input with `write out %text% to %variable%`, end it with `stop process %variable%`. Not for a command whose output the step waits for: that is RunTerminal.")]
		[Example("start process \"claude\", parameters [\"-p\", \"--verbose\"], on output line call OnLine, on exit call OnExit, write to %bot%", @"appExecutableName=""claude"", parameters=[""-p"",""--verbose""], onOutputLine={Name=""OnLine""}, onExit={Name=""OnExit""}, ReturnValues = %bot%")]
		public async Task<(PlangProcess?, IError?)> StartProcess(
			[Description("The program to run, e.g. claude, or a full path such as /usr/bin/node")] string appExecutableName,
			[Description("The arguments handed to the program, one entry each, in the order the step writes them")] List<string>? parameters = null,
			[Description("The directory the program runs in, stated by the step as e.g. `working dir: \"/srv/app\"`")] string? pathToWorkingDirInTerminal = null,
			[Description("The goal called for each line on standard output, `on output line call X`")] GoalToCallInfo? onOutputLine = null,
			[Description("The goal called for each line on standard error, `on error line call X`")] GoalToCallInfo? onErrorLine = null,
			[Description("The goal called when the program has ended, `on exit call X`")] GoalToCallInfo? onExit = null,
			[Description("A name for the process, shown in runtime stats; the program name when the step gives none")] string? name = null)
		{
			var startInfo = new ProcessStartInfo { FileName = ResolveExecutable(appExecutableName), WorkingDirectory = ResolveWorkingDir(pathToWorkingDirInTerminal) };
			foreach (var parameter in parameters ?? []) if (parameter != null) startInfo.ArgumentList.Add(parameter);

			var root = engine;
			while (root.ParentEngine != null) root = root.ParentEngine;

			var callingGoal = goal;
			var callingStep = goalStep;
			PlangProcess? plangProcess = null;

			async Task Call(GoalToCallInfo goalToCall, string variableName, object value)
			{
				// The goals run as the app's own, each call on a context of its own: not on the context of the step that
				// started the process, which may be a request that ended long ago, and not shared between the output and
				// the error stream, whose goals run at the same time.
				var processContext = new PLangContext(MemoryStack.New(root.Container, root), root, ExecutionMode.Console);
				processContext.CallingStep = callingStep;
				processContext.CallStack.EnterGoal(callingGoal);
				contextAccessor.Current = processContext;
				var call = new GoalToCallInfo(goalToCall.Name, new Dictionary<string, object?>(goalToCall.Parameters ?? new())) { Path = goalToCall.Path };
				call.Parameters[variableName] = value;
				call.Parameters["process"] = plangProcess;
				using var origin = RunOrigin.App();
				var (_, task, error) = await pseudoRuntime.RunGoal(root, contextAccessor, fileSystem.RelativeAppPath, call, callingGoal, waitForExecution: false);
				if (error != null)
				{
					logger.LogError($"{goalToCall.Name} for process {plangProcess?.Name}: {error.Message}");
					return;
				}
				if (task is Task running) await running;
			}

			try
			{
				plangProcess = PlangProcess.Start(startInfo, name ?? appExecutableName,
					onOutputLine == null ? null : line => Call(onOutputLine, "line", line),
					onErrorLine == null ? null : line => Call(onErrorLine, "line", line),
					onExit == null ? null : exitCode => Call(onExit, "exitCode", exitCode));
			}
			catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
			{
				return (null, new ProgramError($"Could not start {appExecutableName}: {ex.Message}", goalStep, function, Exception: ex));
			}

			var registry = root.KeepAliveRegistry;
			registry.Add(ProcessKeepAliveKind, plangProcess.Name, plangProcess);
			var started = plangProcess;
			_ = started.Completion.ContinueWith(_ => registry.Remove(started, ProcessKeepAliveKind, dispose: false), TaskScheduler.Default);

			return (plangProcess, null);
		}

		[Description("End a program started with `start process`, e.g. `stop process %bot%`. Its onExit goal is called.")]
		[Example("stop process %bot%", @"process=""%bot%""")]
		public async Task<IError?> StopProcess(object process)
		{
			if (process is not PlangProcess plangProcess)
			{
				return new ProgramError($"{process ?? "null"} is not a process started with `start process`", goalStep, function);
			}
			plangProcess.Stop();
			await Task.WhenAny(plangProcess.Completion, Task.Delay(5000));
			return null;
		}

		private string ResolveWorkingDir(string? pathToWorkingDirInTerminal)
		{
			if (!string.IsNullOrWhiteSpace(pathToWorkingDirInTerminal)) return GetPath(pathToWorkingDirInTerminal);
			if (Goal != null && Goal.IsSystem && context.CallingStep != null) return context.CallingStep.Goal.AbsoluteGoalFolderPath;
			return (Goal != null) ? Goal.AbsoluteGoalFolderPath : fileSystem.GoalsPath;
		}

		private string ResolveExecutable(string appExecutableName)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && string.IsNullOrEmpty(Path.GetExtension(appExecutableName)))
			{
				appExecutableName = appExecutableName + ".exe";
			}
			var fileNameWithPath = GetPath(appExecutableName);
			if (fileSystem.File.Exists(fileNameWithPath)) return fileNameWithPath;
			fileNameWithPath = GetPath(Path.Join("/bin", appExecutableName));
			if (fileSystem.File.Exists(fileNameWithPath)) return fileNameWithPath;
			// not found as a file, so it must be on PATH
			return appExecutableName;
		}

		public async Task Read(string? variableName)
		{
			throw new NotImplementedException("Read is not implemented");
		}

		[Description("Run an executable and return its output. Parameter strings should not be escaped.")]
		[Example("terminal git --status, write to %output%", @"appExecutableName=git, parameters=""--status"", ReturnValues = %output%")]
		[Example("terminal /usr/bin/du, parameters: [%dir%] working dir: \"/srv\", hide terminal, write to %usage%", @"appExecutableName=""/usr/bin/du"", parameters=""%dir%"", pathToWorkingDirInTerminal=""/srv"", hideTerminal=true, ReturnValues = %usage%")]
		[Example("terminal df, parameters: [\"-P\"], parse with \"/parsers/df.json\", write to %disks%", @"appExecutableName=""df"", parameters=""-P"", parseWith=""/parsers/df.json"", ReturnValues = %disks%")]
		public async Task<(object?, IError?, Properties?)> RunTerminal(
			[Description("The program to run, e.g. git in `terminal git --status`, or a full path such as /usr/bin/ffmpeg")] string appExecutableName,
			[Description("The arguments handed to the program, one entry each, in the order the step writes them")] List<string>? parameters = null,
			[Description("The directory the program runs in, stated by the step as e.g. `working dir: \"/srv/app\"`")] string? pathToWorkingDirInTerminal = null,
			// These two are hidden from the builder. A step saying `write to %output%` kept coming
			// back with %output% here instead of as a return value, which streams the output away
			// line by line and returns nothing: the variable ends up holding the last line. Saying
			// so in the description got it from 8 builds in 12 down to 2, not to 0. A parameter the
			// builder cannot see cannot be filled by mistake, so streaming is its own method now.
			// They stay on the signature, unhidden from the runtime, so every .pr already written
			// keeps binding them.
			[LlmIgnore] [HandlesVariable] string? variableNameForDeltaOnStandardStream = null,
			[LlmIgnore] [HandlesVariable] string? variableNameForDeltaOnErrorStream = null,
			[Description("true when the step says the terminal window should not be shown, e.g. `hide terminal`. false otherwise")] bool hideTerminal = false,
			[Description("Path to a JSON template file that turns the program's text output into objects, stated by the step as e.g. `parse with \"/parsers/df.json\"`. Null when the step does not say parse with")] string? parseWith = null
			)
		{
			TerminalParser? parser = null;
			if (!string.IsNullOrWhiteSpace(parseWith))
			{
				var templatePath = GetPath(parseWith);
				if (!fileSystem.File.Exists(templatePath))
				{
					return (null, new ProgramError($"Parse template not found: {parseWith}. Looked for {templatePath}", goalStep, function), null);
				}

				string? parserError;
				(parser, parserError) = TerminalParser.Create(fileSystem.File.ReadAllText(templatePath), parseWith);
				if (parser == null)
				{
					return (null, new ProgramError(parserError!, goalStep, function), null);
				}
			}
			var streamSession = parser?.NewSession();

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

					// An argument is text: a number or a boolean (`plang exec Y port=8091 restart=true`) is written as text, a boolean in lower case
					startInfo.ArgumentList.Add(parameter);
				}
			}

			// Encoding.UTF8 carries a preamble and Process.Start writes it to the redirected stdin. A
			// child that finishes first, any `sh -c` of a few commands, closed the pipe and Start itself
			// threw "Broken pipe", which the try/catch around Close below cannot reach. No preamble, no write.
			startInfo.StandardInputEncoding = new UTF8Encoding(false);
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

			var stderrMode = parser?.Stderr ?? TerminalParser.StderrMode.Error;
			// Both streams are read on their own threads. One lock covers the shared buffer and the parse
			// session, so with stderr "output" the lines land in the buffer in the order they arrive.
			// That is arrival order across the two streams, not the order the program wrote them.
			var gate = new object();

			void TakeOutputLine(string data)
			{
				if (!string.IsNullOrEmpty(variableNameForDeltaOnStandardStream))
				{
					object? delta = parser == null ? data : parser.ParseStreamLine(data, streamSession!);
					if (delta != null)
					{
						memoryStack.Put(variableNameForDeltaOnStandardStream, delta, goalStep: goalStep);
					}
				}

				dataOutput.Append(data + Environment.NewLine);
			}

			process.OutputDataReceived += (sender, e) =>
			{
				if (string.IsNullOrWhiteSpace(e.Data)) return;

				lock (gate)
				{
					TakeOutputLine(e.Data);
				}

				logger.LogTrace(e.Data);
			};


			process.ErrorDataReceived += (sender, e) =>
			{
				if (string.IsNullOrWhiteSpace(e.Data)) return;

				lock (gate)
				{
					errorOutput.Append(e.Data + Environment.NewLine);

					if (stderrMode == TerminalParser.StderrMode.Output)
					{
						TakeOutputLine(e.Data);
					}
					else if (stderrMode == TerminalParser.StderrMode.Error && !string.IsNullOrEmpty(variableNameForDeltaOnErrorStream))
					{
						memoryStack.Put(variableNameForDeltaOnErrorStream, e.Data, goalStep: goalStep);
					}
				}

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
			// Nothing is written to stdin, so closing it only signals end of input. On a child that
			// has already exited the close raises EPIPE, "Broken pipe", and the step fails although
			// the command ran and its output was read. Any command fast enough to finish first hits
			// it: `ls` failed every time in a container, the same `ls` behind `sleep 1` never did.
			// The close is tidiness and must not be able to fail the step.
			try
			{
				process.StandardInput.Close();
			}
			catch (IOException)
			{
			}
			// Close the input stream to signal completion
			if (goalStep.WaitForExecution)
			{
				await process.WaitForExitAsync();
				// WaitForExitAsync can return while the async readers still hold lines. The parameterless
				// WaitForExit waits until both streams reached end of file.
				process.WaitForExit();
			}
			else
			{
				KeepAlive(process, "Process");
			}

			IError? error = null;
			int? exitCode = goalStep.WaitForExecution ? process.ExitCode : null;
			if (exitCode != null)
			{
				properties.Add(new ObjectValue("ExitCode", exitCode.Value));
			}

			// The step is an error when, and only when, the exit code is not ok: 0, or the codes of the parser. Output on stderr with
			// an ok exit code is not an error (many programs write progress and warnings there), and a non-zero exit code always is,
			// whether or not the program wrote to stderr. The exit code stays available as %out!ExitCode%.
			var okExitCodes = parser?.OkExitCodes ?? new[] { 0 };
			bool badExitCode = exitCode != null && !okExitCodes.Contains(exitCode.Value);
			if (badExitCode)
			{
				var tail = string.Join("\n", errorOutput.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).TakeLast(5).Select(l => l.TrimEnd('\r')));
				error = new ProgramError($"Command: {string.Join(" ", startInfo.ArgumentList)}\nExit code {exitCode}, expected one of [{string.Join(", ", okExitCodes)}]\n{tail}", goalStep, function);
			}
			logger.LogTrace("Done with TerminalModule");

			if (parser != null && goalStep.WaitForExecution)
			{
				var parsed = parser.ParseAll(dataOutput.ToString());
				properties.Add(new ObjectValue("Unmatched", new JArray(parsed.Unmatched)));
				return (parsed.Value, error, properties);
			}

			// The line break after the last line is not part of the value, as in a shell's $(...): `date +%Y` gives "2026",
			// and a name or path read this way can be used as it is.
			return (dataOutput.ToString().TrimEnd('\r', '\n'), error, properties);
		}

		[Description("Run an executable that streams its output while it runs, for a step that names a variable to receive each line as it arrives. A plain `write to %variable%` is the finished output and belongs to RunTerminal, not here: this method is only for a step that names a separate variable for the running output, e.g. `terminal ffmpeg ..., %delta%`.")]
		[Example("terminal /usr/bin/rsync -av src dst, %line%, %problem%, write to %summary%", @"appExecutableName=""/usr/bin/rsync"", parameters=""-av"",""src"",""dst"", variableNameForDeltaOnStandardStream=""%line%"", variableNameForDeltaOnErrorStream=""%problem%"", ReturnValues = %summary%")]
		[Example("terminal ffmpeg -i in.mp4 out.mp4, %line%, parse with \"/parsers/ffmpeg.json\", write to %info%", @"appExecutableName=""ffmpeg"", parameters=""-i"",""in.mp4"",""out.mp4"", variableNameForDeltaOnStandardStream=""%line%"", parseWith=""/parsers/ffmpeg.json"", ReturnValues = %info%")]
		public async Task<(object?, IError?, Properties?)> RunTerminalStreaming(
			[Description("The program to run, e.g. ffmpeg, or a full path such as /usr/bin/ffmpeg")] string appExecutableName,
			[Description("The variable that receives standard output line by line as it arrives")] [HandlesVariable] string variableNameForDeltaOnStandardStream,
			[Description("The arguments handed to the program, one entry each, in the order the step writes them")] List<string>? parameters = null,
			[Description("The directory the program runs in, stated by the step as e.g. `working dir: \"/srv/app\"`")] string? pathToWorkingDirInTerminal = null,
			[Description("The variable that receives the error stream line by line, when the step names a second one")] [HandlesVariable] string? variableNameForDeltaOnErrorStream = null,
			[Description("true when the step says the terminal window should not be shown, e.g. `hide terminal`. false otherwise")] bool hideTerminal = false,
			[Description("Path to a JSON template file that turns each output line into an object for the streaming variable, and the whole output into the return value, stated by the step as e.g. `parse with \"/parsers/ffmpeg.json\"`. Null when the step does not say parse with")] string? parseWith = null
			)
		{
			return await RunTerminal(appExecutableName, parameters, pathToWorkingDirInTerminal,
				variableNameForDeltaOnStandardStream, variableNameForDeltaOnErrorStream, hideTerminal, parseWith);
		}

	}
}

