using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace PLang.Modules.TerminalModule
{
	/// <summary>
	/// A program started by `start process`, running beside the app. It is a value: `write to %bot%` holds it, and
	/// `write out %line% to %bot%` writes a line to its standard input, `stop process %bot%` ends it. Each line it writes
	/// to standard output or error is handed to onLine/onErrorLine one at a time, in order; onExit is called once both
	/// streams are read to the end, with the exit code.
	/// Every running process is listed here, so a drain and the end of the app stop them.
	/// </summary>
	// Not IDisposable on purpose: a goal that gets the process as %process% disposes its disposable variables when it
	// ends, which would kill the program after its first output line. A process ends by `stop process`, a drain or the
	// end of the app.
	public sealed class PlangProcess : PLang.Models.ILiveValue
	{
		private static readonly ConcurrentDictionary<int, PlangProcess> running = new();
		private static int processExitHooked;

		[JsonIgnore]
		public Process Process { get; }
		public int Id { get; }
		public string Name { get; }
		public DateTime StartedUtc { get; } = DateTime.UtcNow;
		public int? ExitCode { get; private set; }
		/// <summary>Why reading the program's output failed, when it did; the exit code is then -1.</summary>
		public string? Error { get; private set; }
		public bool IsRunning => ExitCode == null;

		[JsonIgnore]
		public Task Completion { get; private set; } = Task.CompletedTask;

		private readonly SemaphoreSlim writeGate = new(1, 1);

		private PlangProcess(Process process, string name)
		{
			Process = process;
			Id = process.Id;
			Name = name;
		}

		/// <summary>
		/// Starts the program. onLine and onErrorLine are awaited for each line before the next line of the same stream
		/// is read, so no line is lost or reordered; onExit gets the exit code after both streams have ended.
		/// </summary>
		public static PlangProcess Start(ProcessStartInfo startInfo, string name, Func<string, Task>? onLine, Func<string, Task>? onErrorLine, Func<int, Task>? onExit)
		{
			startInfo.RedirectStandardInput = true;
			startInfo.RedirectStandardOutput = true;
			startInfo.RedirectStandardError = true;
			startInfo.UseShellExecute = false;
			startInfo.StandardInputEncoding = new UTF8Encoding(false);
			startInfo.StandardOutputEncoding = Encoding.UTF8;
			startInfo.StandardErrorEncoding = Encoding.UTF8;

			var process = new Process { StartInfo = startInfo };
			process.Start();

			var plangProcess = new PlangProcess(process, name);
			running[plangProcess.Id] = plangProcess;
			HookProcessExit();

			var stdout = ReadLines(process.StandardOutput, onLine);
			var stderr = ReadLines(process.StandardError, onErrorLine);
			plangProcess.Completion = Task.Run(async () =>
			{
				try
				{
					await Task.WhenAll(stdout, stderr);
					await process.WaitForExitAsync();
					plangProcess.ExitCode = process.ExitCode;
				}
				catch (Exception ex)
				{
					plangProcess.Error = ex.GetType().Name + ": " + ex.Message;
					plangProcess.ExitCode ??= -1;
				}
				finally
				{
					running.TryRemove(plangProcess.Id, out _);
				}
				if (onExit != null) await onExit(plangProcess.ExitCode ?? -1);
			});
			return plangProcess;
		}

		private static async Task ReadLines(StreamReader reader, Func<string, Task>? onLine)
		{
			string? line;
			while ((line = await reader.ReadLineAsync()) != null)
			{
				if (onLine == null) continue;
				try { await onLine(line); } catch { }
			}
		}

		/// <summary>Writes text and a line break to the program's standard input and flushes it.</summary>
		public async Task WriteLine(string text)
		{
			if (!IsRunning) throw new InvalidOperationException($"Process {Name} ({Id}) has exited with code {ExitCode}");
			await writeGate.WaitAsync();
			try
			{
				await Process.StandardInput.WriteAsync(text);
				await Process.StandardInput.WriteAsync('\n');
				await Process.StandardInput.FlushAsync();
			}
			finally
			{
				writeGate.Release();
			}
		}

		/// <summary>Ends the program and every process it started. Does nothing when it has already exited.</summary>
		public void Stop()
		{
			try
			{
				if (!Process.HasExited) Process.Kill(entireProcessTree: true);
			}
			catch (InvalidOperationException) { }
		}

		/// <summary>Stops every running process; returns how many were stopped.</summary>
		public static int StopAll()
		{
			int count = 0;
			foreach (var process in running.Values)
			{
				process.Stop();
				count++;
			}
			return count;
		}

		private static void HookProcessExit()
		{
			if (Interlocked.Exchange(ref processExitHooked, 1) == 1) return;
			AppDomain.CurrentDomain.ProcessExit += (_, _) => StopAll();
		}

		public override string ToString() => $"process {Name} ({Id}){(IsRunning ? "" : $", exited {ExitCode}")}";
	}
}
