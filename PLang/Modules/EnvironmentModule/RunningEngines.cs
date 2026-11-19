using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace PLang.Modules.EnvironmentModule;

// What `get running engines` returns. Written as JSON the names are snake case.

/// <summary>
/// One engine: a rented one (serving a web request, a "dont wait" or scheduled run) or the root engine.
/// State is "running" while its call stack has frames, "idle" when it has none (rented, nothing running yet or any more).
/// Goal and Step are where it is now: the goal file and the step as goal file:line. CalledFrom is the step that rented it.
/// The size fields are only set when sizes were requested; SerializeBytes is null for an engine that was not measured.
/// </summary>
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record RunningEngine(string Id, string Name, bool IsRoot, string State, string? Goal, string? Step, string? StepText, string? CalledFrom,
	DateTime StartedUtc, double RunningSeconds, int? ThreadId, int MemoryStackVariables, EngineCallStack CallStack,
	long? SerializeBytes = null, bool? Capped = null, bool? Truncated = null, List<VariableSize>? LargestVariables = null, List<VariableSizeError>? SizeErrors = null,
		EngineRequest? Request = null, int Tasks = 0);

/// <summary>The web request an engine is serving: method and path (no query string, it may hold secrets).</summary>
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record EngineRequest(string Method, string Path);

/// <summary>
/// The engine's call stack, current frame first. ExecutedSteps and RunningMs add up every frame, the cost of the stack:
/// an outer frame's time includes the frames it called.
/// </summary>
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record EngineCallStack(int Depth, int ExecutedSteps, double RunningMs, List<EngineStackFrame> Frames);

[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record EngineStackFrame(string? Goal, string? Step, double RunningMs, int ExecutedSteps);

/// <summary>A variable's size serialized as JSON. Capped: counting stopped at the variable or engine limit, the size is at least Bytes.</summary>
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record VariableSize(string Name, long Bytes, bool Capped);

[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record VariableSizeError(string Name, string Error);

/// <summary>The largest engine the last `get running engines, with sizes` measured, among the engines still running.</summary>
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record LargestEngine(string Id, string Name, long SerializeBytes, DateTime MeasuredUtc);

/// <summary>
/// Caps on measuring sizes: a variable stops counting at VariableCapBytes, an engine at EngineCapBytes (Capped),
/// and measuring stops for all engines after TimeLimit (Truncated; the engines after it get no size).
/// </summary>
public record EngineSizeLimits(long VariableCapBytes, long EngineCapBytes, TimeSpan TimeLimit)
{
	public static readonly EngineSizeLimits Default = new(10L * 1024 * 1024, 50L * 1024 * 1024, TimeSpan.FromMilliseconds(200));
}

public static class RunningEnginesReader
{
	public const int LargestVariablesCount = 5;
	private const int MaxSizeErrors = 5;

	private record MeasuredSize(long Bytes, DateTime MeasuredUtc);

	// The last measured size of each rented engine, by its registry entry: an engine reused from the pool gets a new
	// entry, so an old measurement never shows for a new rent, and a measurement goes when its entry is collected.
	private static readonly ConditionalWeakTable<KeepAliveEntry, MeasuredSize> lastMeasured = new();

	/// <summary>
	/// The rented engines (and the root engine when includeRoot), longest running first, or largest first when withSizes.
	/// Only the top ones are returned (all when top is 0 or less). Reads each engine's call stack and memory stack from
	/// this thread while the engine keeps running; nothing is locked or paused.
	/// </summary>
	public static List<RunningEngine> Read(IEngine engine, bool withSizes = false, int top = 20, bool includeRoot = false, EngineSizeLimits? limits = null)
	{
		var now = DateTime.UtcNow;
		var root = GetRootEngine(engine);

		var items = new List<(KeepAliveEntry? Entry, IEngine Engine, DateTime StartedUtc)>();
		foreach (var entry in engine.KeepAliveRegistry.GetEngineSnapshot())
		{
			if (entry.Instance is IEngine rented) items.Add((entry, rented, entry.StartedUtc));
		}
		if (includeRoot) items.Add((null, root, GetProcessStartUtc(now)));

		items = items.OrderByDescending(p => now - p.StartedUtc).ToList();

		var engines = new List<RunningEngine>();
		if (!withSizes)
		{
			foreach (var item in items)
			{
				engines.Add(Describe(item.Engine, item.Entry == null, item.StartedUtc, now));
			}
			return Top(engines, top);
		}

		limits ??= EngineSizeLimits.Default;
		var stopwatch = Stopwatch.StartNew();
		// Measured longest running first, so when time runs out the ones without a size are the newest.
		foreach (var item in items)
		{
			var described = Describe(item.Engine, item.Entry == null, item.StartedUtc, now);
			if (stopwatch.Elapsed >= limits.TimeLimit)
			{
				engines.Add(described with { Truncated = true });
				continue;
			}

			var size = MeasureMemoryStack(item.Engine.RunningContext?.MemoryStack, limits, stopwatch);
			if (item.Entry != null && !size.Truncated)
			{
				lastMeasured.AddOrUpdate(item.Entry, new MeasuredSize(size.Bytes, DateTime.UtcNow));
			}
			engines.Add(described with
			{
				SerializeBytes = size.Bytes,
				Capped = size.Capped,
				Truncated = size.Truncated,
				LargestVariables = size.Largest,
				SizeErrors = size.Errors.Count > 0 ? size.Errors : null
			});
		}

		engines = engines.OrderByDescending(p => p.SerializeBytes ?? -1).ThenByDescending(p => p.RunningSeconds).ToList();
		return Top(engines, top);
	}

	private static List<RunningEngine> Top(List<RunningEngine> engines, int top)
	{
		return (top > 0) ? engines.Take(top).ToList() : engines;
	}

	/// <summary>The engine's id, name, state, where it is and its call stack, read without serializing anything.</summary>
	public static RunningEngine Describe(IEngine engine, bool isRoot, DateTime startedUtc, DateTime now)
	{
		var context = engine.RunningContext;
		var frames = context?.CallStack?.GetFrames() ?? Array.Empty<CallStackFrame>();

		var stackFrames = new List<EngineStackFrame>(frames.Count);
		int executedSteps = 0;
		double runningMs = 0;
		foreach (var frame in frames)
		{
			double frameMs = Math.Round(frame.Duration.TotalMilliseconds, 1);
			int frameSteps = frame.ExecutedStepCount;
			executedSteps += frameSteps;
			runningMs += frameMs;
			stackFrames.Add(new EngineStackFrame(GoalPath(frame.Goal), StepLocation(frame.CurrentStep, frame.Goal), frameMs, frameSteps));
		}

		var current = frames.Count > 0 ? frames[0] : null;
		string state = current != null ? "running" : "idle";

		return new RunningEngine(engine.Id, engine.Name, isRoot, state,
			current == null ? null : GoalPath(current.Goal),
			current == null ? null : StepLocation(current.CurrentStep, current.Goal),
			current?.CurrentStep?.Text,
			StepLocation(engine.RentedFrom, null),
			startedUtc, Math.Round((now - startedUtc).TotalSeconds, 1), engine.RunningThreadId,
			context?.MemoryStack?.VariableCount ?? 0,
			new EngineCallStack(frames.Count, executedSteps, Math.Round(runningMs, 1), stackFrames),
			Request: ReadRequest(context),
			Tasks: CountTasks(engine));
	}

	private static EngineRequest? ReadRequest(PLangContext? context)
	{
		try
		{
			var request = context?.HttpContext?.Request;
			return request == null ? null : new EngineRequest(request.Method, request.Path.Value ?? "");
		}
		catch
		{
			// the request was disposed while the engine finished
			return null;
		}
	}

	/// <summary>The goals this engine started without waiting ("dont wait", scheduled runs) that are still running.</summary>
	private static int CountTasks(IEngine engine)
	{
		try
		{
			return engine.KeepAliveRegistry.GetSnapshot().Count(p => p.Instance is PlangTask task && !task.IsCompleted && task.CallingEngineId == engine.Id);
		}
		catch
		{
			return 0;
		}
	}

	private static string? GoalPath(Goal? goal)
	{
		if (goal == null) return null;
		return string.IsNullOrEmpty(goal.RelativeGoalPath) ? goal.GoalName : goal.RelativeGoalPath;
	}

	private static string? StepLocation(GoalStep? step, Goal? goal)
	{
		if (step == null) return null;
		// The goal's path is normalized when the goal is loaded, the step's copy is kept as written in the .pr file.
		var path = GoalPath(step.Goal ?? goal);
		if (string.IsNullOrEmpty(path)) path = step.RelativeGoalPath;
		return $"{path}:{step.LineNumber}";
	}

	/// <summary>
	/// The last measured size of each rented engine still running, for `get runtime stats`, which never measures.
	/// </summary>
	public static LargestEngine? GetLargestMeasured(List<KeepAliveEntry> engineEntries)
	{
		LargestEngine? largest = null;
		foreach (var entry in engineEntries)
		{
			if (!lastMeasured.TryGetValue(entry, out var size)) continue;
			if (largest != null && largest.SerializeBytes >= size.Bytes) continue;
			var name = (entry.Instance as IEngine)?.Name ?? entry.Name;
			largest = new LargestEngine(entry.Name, name, size.Bytes, size.MeasuredUtc);
		}
		return largest;
	}

	public record MemoryStackSize(long Bytes, bool Capped, bool Truncated, List<VariableSize> Largest, List<VariableSizeError> Errors);

	/// <summary>
	/// Serializes every user variable of the memory stack to a stream that only counts bytes, one variable at a time.
	/// System variables (Now, GUID, ...) and computed ones are skipped, and so are runtime objects that are not data
	/// (engines, contexts, streams, tasks, ...). A variable stops counting at the variable cap, the engine at the engine
	/// cap (Capped), and everything stops when the stopwatch passes the time limit (Truncated). A variable that fails
	/// to serialize, e.g. a list the engine is changing right now, is reported in Errors; this never throws.
	/// </summary>
	public static MemoryStackSize MeasureMemoryStack(MemoryStack? memoryStack, EngineSizeLimits limits, Stopwatch stopwatch)
	{
		var sizes = new List<VariableSize>();
		var errors = new List<VariableSizeError>();
		if (memoryStack == null) return new MemoryStackSize(0, false, false, sizes, errors);

		long total = 0;
		bool engineCapped = false, truncated = false;

		List<ObjectValue> variables;
		try
		{
			variables = memoryStack.GetMemoryStack().ToList();
		}
		catch (Exception ex)
		{
			errors.Add(new VariableSizeError("*", ex.Message));
			return new MemoryStackSize(0, false, false, sizes, errors);
		}

		foreach (var variable in variables)
		{
			if (variable.IsSystemVariable || variable is DynamicObjectValue) continue;
			if (stopwatch.Elapsed >= limits.TimeLimit) { truncated = true; break; }

			long remainingForEngine = limits.EngineCapBytes - total;
			long cap = Math.Min(limits.VariableCapBytes, remainingForEngine);

			object? value;
			try
			{
				value = variable.Value;
			}
			catch (Exception ex)
			{
				AddError(errors, variable.Name, ex);
				continue;
			}
			if (value == null || IsRuntimeObject(value)) continue;

			var counter = new CountingStream(cap, stopwatch, limits.TimeLimit);
			try
			{
				SerializeTo(counter, value);
			}
			catch (SizeCapReachedException) { }
			catch (TimeLimitReachedException) { }
			catch (Exception ex)
			{
				AddError(errors, variable.Name, ex);
			}

			total += counter.Count;
			sizes.Add(new VariableSize(variable.Name, counter.Count, counter.CapReached));

			if (counter.TimeLimitReached) { truncated = true; break; }
			if (counter.CapReached && cap == remainingForEngine) { engineCapped = true; break; }
		}

		var largest = sizes.OrderByDescending(p => p.Bytes).Take(LargestVariablesCount).ToList();
		return new MemoryStackSize(total, engineCapped || sizes.Any(p => p.Capped), truncated, largest, errors);
	}

	private static void AddError(List<VariableSizeError> errors, string name, Exception ex)
	{
		if (errors.Count < MaxSizeErrors) errors.Add(new VariableSizeError(name, ex.GetType().Name + ": " + ex.Message));
	}

	private static void SerializeTo(Stream stream, object value)
	{
		var serializer = new JsonSerializer
		{
			ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
		};
		// A member that cannot be read (a property getter that throws, a collection changed while it is enumerated)
		// is left out and the rest counted. The caps are not errors to skip: they end the variable.
		serializer.Error += (sender, args) =>
		{
			if (args.ErrorContext.Error is SizeCapReachedException or TimeLimitReachedException) return;
			args.ErrorContext.Handled = true;
		};

		using var streamWriter = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true);
		using var jsonWriter = new JsonTextWriter(streamWriter);
		serializer.Serialize(jsonWriter, value);
		jsonWriter.Flush();
	}

	/// <summary>Objects of the runtime that are not data: serializing them is meaningless or walks the whole runtime.</summary>
	private static bool IsRuntimeObject(object value)
	{
		return value is IEngine || value is PLangContext || value is MemoryStack || value is LightInject.IServiceContainer
			|| value is Stream || value is Delegate || value is Task || value is System.Data.Common.DbConnection
			|| value is Microsoft.AspNetCore.Http.HttpContext || value is CancellationTokenSource || value is Thread;
	}

	private static DateTime GetProcessStartUtc(DateTime fallback)
	{
		try
		{
			using var process = Process.GetCurrentProcess();
			return process.StartTime.ToUniversalTime();
		}
		catch
		{
			return fallback;
		}
	}

	private static IEngine GetRootEngine(IEngine engine)
	{
		while (engine.ParentEngine != null) engine = engine.ParentEngine;
		return engine;
	}

	private sealed class SizeCapReachedException : Exception { }
	private sealed class TimeLimitReachedException : Exception { }

	/// <summary>
	/// A write-only stream that counts the bytes written and keeps none. Throws once the cap or the time limit is passed,
	/// which ends the serialization writing to it.
	/// </summary>
	private sealed class CountingStream : Stream
	{
		private readonly long cap;
		private readonly Stopwatch stopwatch;
		private readonly TimeSpan timeLimit;

		public long Count { get; private set; }
		public bool CapReached { get; private set; }
		public bool TimeLimitReached { get; private set; }

		public CountingStream(long cap, Stopwatch stopwatch, TimeSpan timeLimit)
		{
			this.cap = cap;
			this.stopwatch = stopwatch;
			this.timeLimit = timeLimit;
		}

		public override void Write(byte[] buffer, int offset, int count) => Add(count);
		public override void Write(ReadOnlySpan<byte> buffer) => Add(buffer.Length);
		public override void WriteByte(byte value) => Add(1);

		private void Add(int count)
		{
			if (CapReached) throw new SizeCapReachedException();
			if (TimeLimitReached) throw new TimeLimitReachedException();

			if (Count + count > cap)
			{
				Count = cap;
				CapReached = true;
				throw new SizeCapReachedException();
			}
			Count += count;
			if (stopwatch.Elapsed >= timeLimit)
			{
				TimeLimitReached = true;
				throw new TimeLimitReachedException();
			}
		}

		public override bool CanRead => false;
		public override bool CanSeek => false;
		public override bool CanWrite => true;
		public override long Length => Count;
		public override long Position { get => Count; set => throw new NotSupportedException(); }
		public override void Flush() { }
		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
	}
}
