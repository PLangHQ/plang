using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PLang.Events;
using PLang.Interfaces;
using PLang.Runtime;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PLang.Modules.EnvironmentModule;

// What `get runtime stats` returns. Written as JSON the names are snake case. Sizes are in MB with two decimals.

[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record RuntimeStats(DateTime TimestampUtc, GcRuntimeStats Gc, ProcessRuntimeStats Process, PlangRuntimeStats Plang);

/// <summary>
/// The managed heap. HeapMb is what the GC counts as live right now (garbage not collected yet included),
/// HeapAfterCollectMb is the live heap after a forced full collection and is null when none was forced.
/// CommittedMb is the memory the GC holds from the OS for its heap.
/// </summary>
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record GcRuntimeStats(double HeapMb, double? HeapAfterCollectMb, int Gen0Collections, int Gen1Collections, int Gen2Collections,
	double TotalAllocatedMb, double HeapSizeMb, double FragmentedMb, double CommittedMb, double LargeObjectHeapMb, double PinnedObjectHeapMb,
	long FinalizationPendingCount, long PinnedObjectsCount);

/// <summary>
/// The process as the OS sees it. RssAnonMb (Linux) is memory the process allocated itself: managed heap, native allocations,
/// thread stacks. NotGcMb is RSS minus what the GC has committed: memory held outside the managed heap (native libraries,
/// loaded code, stacks). Growth there and not in the GC numbers points at native code, not at objects PLang holds.
/// </summary>
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record ProcessRuntimeStats(double RssMb, double? RssAnonMb, double? RssFileMb, double? SwapMb, double PrivateMb, double VirtualMb, double NotGcMb,
	int Threads, int? OpenFiles, int HandleCount, int ThreadPoolThreads, long ThreadPoolPendingWorkItems, long Timers, int LoadedAssemblies);

/// <summary>Engines the root engine's pool has created and not disposed (Total), rented out (Rented) and waiting (Free).</summary>
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record EnginePoolStats(int Total, int Rented, int Free);

/// <summary>
/// What PLang holds. CallStackDepth is the frame count of the current context's call stack, CallStackMaxDepth the deepest it has been.
/// KeepAliveEntries and RunningTasks count what keeps the app running, RunningTasks by kind (WaitForExecution for
/// "dont wait" goals, Schedule, Webserver, ...). EnginesRunning counts the rented engines, OldestRunningEngineSeconds is
/// how long the oldest has been rented. LargestEngine comes from the last `get running engines, with sizes`; it is
/// null when none of the running engines was measured, sizes are never measured here.
/// Caches is the entry count of every runtime cache and list that grows while the app runs, by name.
/// </summary>
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public record PlangRuntimeStats(int CallStackDepth, int CallStackMaxDepth, int ExecutedStepsOnCallStack, int MemoryStackVariables,
	int KeepAliveEntries, Dictionary<string, int> RunningTasks, EnginePoolStats? EnginePool,
	int EnginesRunning, double OldestRunningEngineSeconds, LargestEngine? LargestEngine,
	int ContextItems, int ContextSharedItems, int ContextEventBindings, int RegexCacheLimit, Dictionary<string, int> Caches,
	bool Standby = false, bool SchedulerEnabled = true, bool Draining = false);

public static class RuntimeStatsReader
{
	private const double Mb = 1024.0 * 1024.0;
	private static double ToMb(long bytes) => Math.Round(bytes / Mb, 2);

	/// <summary>
	/// Reads the numbers. With forceGc a full blocking, compacting collection runs first (large object heap included),
	/// finalizers are run and a second collection frees what they released, so HeapAfterCollectMb is what is really alive.
	/// That stops every thread of the app while it runs.
	/// </summary>
	public static RuntimeStats Read(IEngine engine, PLangContext? context, bool forceGc = false)
	{
		double? heapAfterCollect = null;
		if (forceGc)
		{
			GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
			GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
			GC.WaitForPendingFinalizers();
			GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
			heapAfterCollect = ToMb(GC.GetTotalMemory(false));
		}

		var gc = ReadGc(heapAfterCollect);
		return new RuntimeStats(DateTime.UtcNow, gc, ReadProcess(gc), ReadPlang(engine, context));
	}

	private static GcRuntimeStats ReadGc(double? heapAfterCollect)
	{
		var info = GC.GetGCMemoryInfo(GCKind.Any);
		var generations = info.GenerationInfo;
		// GenerationInfo: 0, 1, 2, then the large object heap (3) and the pinned object heap (4).
		long loh = generations.Length > 3 ? generations[3].SizeAfterBytes : 0;
		long poh = generations.Length > 4 ? generations[4].SizeAfterBytes : 0;

		return new GcRuntimeStats(ToMb(GC.GetTotalMemory(false)), heapAfterCollect,
			GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), ToMb(GC.GetTotalAllocatedBytes(false)),
			ToMb(info.HeapSizeBytes), ToMb(info.FragmentedBytes), ToMb(info.TotalCommittedBytes), ToMb(loh), ToMb(poh),
			info.FinalizationPendingCount, info.PinnedObjectsCount);
	}

	private static ProcessRuntimeStats ReadProcess(GcRuntimeStats gc)
	{
		using var process = Process.GetCurrentProcess();
		double rss = ToMb(process.WorkingSet64);

		double? anon = null, file = null, swap = null;
		int? openFiles = null;
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			var status = HostModule.ProcReader.ReadStatusKb(Environment.ProcessId);
			if (status.TryGetValue("RssAnon", out long anonKb)) anon = Math.Round(anonKb / 1024.0, 2);
			if (status.TryGetValue("RssFile", out long fileKb)) file = Math.Round(fileKb / 1024.0, 2);
			if (status.TryGetValue("VmSwap", out long swapKb)) swap = Math.Round(swapKb / 1024.0, 2);
			openFiles = HostModule.ProcReader.CountOpenFiles(Environment.ProcessId);
		}

		return new ProcessRuntimeStats(rss, anon, file, swap, ToMb(process.PrivateMemorySize64), ToMb(process.VirtualMemorySize64),
			Math.Round(rss - gc.CommittedMb, 2), process.Threads.Count, openFiles, process.HandleCount,
			ThreadPool.ThreadCount, ThreadPool.PendingWorkItemCount, Timer.ActiveCount, AppDomain.CurrentDomain.GetAssemblies().Length);
	}

	private static PlangRuntimeStats ReadPlang(IEngine engine, PLangContext? context)
	{
		var callStack = context?.CallStack;
		int executedSteps = callStack?.GetFrames().Sum(f => f.ExecutedStepCount) ?? 0;

		var registry = engine.KeepAliveRegistry;
		var snapshot = registry?.GetKeepAppAliveSnapshot() ?? new List<KeepAliveEntry>();
		var runningTasks = snapshot.GroupBy(p => p.Kind).ToDictionary(g => g.Key, g => g.Count());

		var engines = registry?.GetEngineSnapshot() ?? new List<KeepAliveEntry>();
		var now = DateTime.UtcNow;
		double oldestEngineSeconds = engines.Count == 0 ? 0 : Math.Round(engines.Max(p => (now - p.StartedUtc).TotalSeconds), 1);

		var pool = GetRootEngine(engine).EnginePool;
		var enginePool = pool == null ? null : new EnginePoolStats(pool.Total, pool.Rented, pool.Free);

		return new PlangRuntimeStats(callStack?.Depth ?? 0, callStack?.MaxDepthSeen ?? 0, executedSteps, context?.MemoryStack?.VariableCount ?? 0,
			snapshot.Count, runningTasks, enginePool,
			engines.Count, oldestEngineSeconds, RunningEnginesReader.GetLargestMeasured(engines),
			context?.Items?.Count ?? 0, context?.SharedItems?.Count ?? 0, context?.EventBindings?.Count ?? 0, Regex.CacheSize,
			GetCacheCounts(engine), Utils.RuntimeMode.Standby, Utils.RuntimeMode.SchedulerEnabled, Utils.RuntimeMode.Draining);
	}

	private static IEngine GetRootEngine(IEngine engine)
	{
		while (engine.ParentEngine != null) engine = engine.ParentEngine;
		return engine;
	}

	/// <summary>
	/// Entry count of every runtime cache, registry and list that can grow while the app runs, by name.
	/// Only counts are read; nothing is enumerated except the weak tables, which have no Count of their own.
	/// </summary>
	public static Dictionary<string, int> GetCacheCounts(IEngine engine)
	{
		var counts = new Dictionary<string, int>();
		void Add(Dictionary<string, int>? more)
		{
			if (more == null) return;
			foreach (var item in more) counts[item.Key] = item.Value;
		}

		var root = GetRootEngine(engine);
		Add(root.PrParser?.GetCacheCounts());
		if (root.GetEventRuntime() is EventRuntime eventRuntime)
		{
			counts["event_runtime_events"] = eventRuntime.RuntimeEventCount;
			counts["event_runtime_builder_events"] = eventRuntime.BuilderEventCount;
		}
		counts["engine_live_connections"] = root.LiveConnections?.Count ?? 0;
		counts["engine_child_engines"] = root.ChildEngines?.Count ?? 0;
		counts["engine_pool_ids"] = root.EnginePool?.engineIds.Count ?? 0;

		Add(Utils.MethodHelper.GetCacheCounts());
		Add(Utils.TypeHelper.GetCacheCounts());
		counts["method_cache"] = Utils.MethodCache.Cache.Count;
		counts["json_parameter_plans"] = JObjectPlan.PlanCount;
		counts["base_program_task_readers"] = BaseProgram.TaskReaderCount;
		counts["base_program_value_tuple_types"] = BaseProgram.ValueTupleTypeCount;
		counts["path_segment_cache"] = Models.ObjectValueConverters.PathSegmentParser.PathCacheCount;
		counts["module_loader_modules"] = Runtime.Startup.ModuleLoader.Modules.Count;
		counts["container_injected_types"] = Container.Instance.InjectedTypeCount;
		counts["container_not_registered_notices"] = Container.Instance.NotifiedAboutNotRegisterCount;
		counts["settings_in_memory_db"] = Services.SettingsService.SqliteSettingsRepository.InMemoryDbCache.Count;
		counts["app_cache_memory_cache"] = (int)System.Runtime.Caching.MemoryCache.Default.GetCount();
		counts["template_parsed"] = TemplateEngineModule.TemplateCache.ParsedCount;
		counts["template_files"] = TemplateEngineModule.TemplateCache.FileCount;
		counts["webserver_listeners"] = WebserverModule.Program.ListenerCount;
		counts["webserver_user_agents"] = WebserverModule.RequestHandler.UserAgentCacheCount;
		counts["webserver_goal_effects_steps"] = WebserverModule.GoalEffects.StepCacheCount;
		counts["webserver_goal_effects_declared"] = WebserverModule.GoalEffects.DeclaredCacheCount;
		counts["sqlite_wal_confirmed"] = DbModule.SqliteJournalMode.WalConfirmedCount;
		counts["sqlite_write_gates"] = DbModule.SqliteWriteGate.GateCount;
		counts["iroh_endpoints"] = IrohModule.Program.EndpointCount;
		return counts;
	}
}
