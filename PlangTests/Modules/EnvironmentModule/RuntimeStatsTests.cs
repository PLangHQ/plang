using LightInject;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Modules.EnvironmentModule;
using ProcReader = PLang.Modules.HostModule.ProcReader;
using Program = PLang.Modules.EnvironmentModule.Program;
using PLang.Runtime;
using PLang.Services.SettingsService;
using PLang.Utils;

namespace PLangTests.Modules.EnvironmentModule
{
	[TestClass]
	public class RuntimeStatsTests
	{
		private (IEngine engine, PLangContext context, KeepAliveRegistry registry) CreateContext()
		{
			var fileSystem = Substitute.For<IPLangFileSystem>();
			var logger = Substitute.For<ILogger>();
			var settings = Substitute.For<ISettings>();
			var contextAccessor = Substitute.For<IPLangContextAccessor>();
			var registry = new KeepAliveRegistry();
			var engine = Substitute.For<IEngine>();
			engine.KeepAliveRegistry.Returns(registry);
			engine.ParentEngine.Returns((IEngine?)null);
			var prParser = new PrParser(fileSystem, logger);
			engine.PrParser.Returns(prParser);

			var runtime = new PseudoRuntime(fileSystem, prParser, logger);
			var memoryStack = new MemoryStack(runtime, engine, settings, new VariableHelper(settings, logger), contextAccessor);
			var context = new PLangContext(memoryStack, engine, ExecutionMode.Console);
			contextAccessor.Current.Returns(context);
			return (engine, context, registry);
		}

		[TestMethod]
		public void Read_ReportsCallStackMemoryStackRunningTasksAndCaches()
		{
			var (engine, context, registry) = CreateContext();
			context.CallStack.EnterGoal(new Goal { GoalName = "Start" });
			context.CallStack.EnterGoal(new Goal { GoalName = "Loop" });
			context.CallStack.EnterGoal(new Goal { GoalName = "Inner" });
			context.CallStack.ExitGoal();
			int variablesBefore = context.MemoryStack.VariableCount;
			context.MemoryStack.Put("name", "plang");

			var running = new TaskCompletionSource<(object? Variables, IError? Error)>().Task;
			var loop = new Goal { GoalName = "Loop", RelativeGoalPath = "/Loop.goal" };
			registry.Add(KeepAliveRegistry.DontWait, "Loop", new PlangTask(running, loop, null));
			registry.Add(KeepAliveRegistry.DontWait, "Loop", new PlangTask(running, loop, null));
			registry.Add("Webserver", "default *:8080", new object());

			var stats = RuntimeStatsReader.Read(engine, context);

			Assert.AreEqual(2, stats.Plang.CallStackDepth);
			Assert.AreEqual(3, stats.Plang.CallStackMaxDepth);
			Assert.AreEqual(variablesBefore + 1, stats.Plang.MemoryStackVariables);
			Assert.AreEqual(3, stats.Plang.KeepAliveEntries);
			Assert.AreEqual(2, stats.Plang.RunningTasks["WaitForExecution"]);
			Assert.AreEqual(1, stats.Plang.RunningTasks["Webserver"]);
			Assert.IsNull(stats.Gc.HeapAfterCollectMb);
			Assert.IsTrue(stats.Gc.HeapMb > 0);
			Assert.IsTrue(stats.Process.RssMb > 0);
			Assert.IsTrue(stats.Process.Threads > 0);
			foreach (var name in new[] { "pr_parser_goal_lookup", "pr_parser_instructions", "method_helper_method_info", "path_segment_cache",
				"type_helper_runtime_type_by_module", "app_cache_memory_cache", "webserver_user_agents", "iroh_endpoints" })
			{
				Assert.IsTrue(stats.Plang.Caches.ContainsKey(name), name);
			}

			var json = JsonConvert.SerializeObject(stats);
			StringAssert.Contains(json, "\"call_stack_max_depth\":3");
			StringAssert.Contains(json, "\"heap_after_collect_mb\":null");
			StringAssert.Contains(json, "\"rss_mb\":");
			StringAssert.Contains(json, "\"engine_pool\":");
		}

		[TestMethod]
		public void Read_ForceGc_MeasuresHeapAfterCollect()
		{
			var (engine, context, _) = CreateContext();
			int gen2Before = GC.CollectionCount(2);

			var stats = RuntimeStatsReader.Read(engine, context, forceGc: true);

			Assert.IsNotNull(stats.Gc.HeapAfterCollectMb);
			Assert.IsTrue(stats.Gc.Gen2Collections >= gen2Before + 2);
		}

		[TestMethod]
		public void Read_WithoutContext_ReportsZeroDepth()
		{
			var (engine, _, _) = CreateContext();

			var stats = RuntimeStatsReader.Read(engine, null);

			Assert.AreEqual(0, stats.Plang.CallStackDepth);
			Assert.AreEqual(0, stats.Plang.MemoryStackVariables);
		}

		[TestMethod]
		public async Task GetCallStackDepth_IsTheCurrentContextsDepth()
		{
			var (engine, context, _) = CreateContext();
			context.CallStack.EnterGoal(new Goal { GoalName = "Start" });
			context.CallStack.EnterGoal(new Goal { GoalName = "Loop" });

			var contextAccessor = Substitute.For<IPLangContextAccessor>();
			contextAccessor.Current.Returns(context);
			var container = Substitute.For<IServiceContainer>();
			container.GetInstance(typeof(ILogger)).Returns(Substitute.For<ILogger>());
			container.GetInstance(typeof(IEngine)).Returns(engine);

			var program = new Program(Substitute.For<ISettings>(), Substitute.For<IPLangFileSystem>(), Substitute.For<ISettingsRepositoryFactory>(), engine);
			program.Init(container, new Goal { GoalName = "Loop" }, new GoalStep(), null!, contextAccessor);

			Assert.AreEqual(2, await program.GetCallStackDepth());
			var stats = await program.GetRuntimeStats();
			Assert.AreEqual(2, stats.Plang.CallStackDepth);
		}

		[TestMethod]
		public void ProcReader_ReadStatusKb_AndCountOpenFiles()
		{
			string procRoot = Path.Join(Path.GetTempPath(), "plang-proc-" + Guid.NewGuid().ToString("N"));
			try
			{
				Directory.CreateDirectory(Path.Join(procRoot, "42", "fd"));
				File.WriteAllText(Path.Join(procRoot, "42", "status"), "Name:\tplang\nVmRSS:\t  204800 kB\nRssAnon:\t  102400 kB\nRssFile:\t   51200 kB\nThreads:\t25\n");
				File.WriteAllText(Path.Join(procRoot, "42", "fd", "0"), "");
				File.WriteAllText(Path.Join(procRoot, "42", "fd", "1"), "");

				var status = ProcReader.ReadStatusKb(42, procRoot);
				Assert.AreEqual(204800, status["VmRSS"]);
				Assert.AreEqual(102400, status["RssAnon"]);
				Assert.AreEqual(51200, status["RssFile"]);
				Assert.IsFalse(status.ContainsKey("Threads"));
				Assert.AreEqual(2, ProcReader.CountOpenFiles(42, procRoot));

				Assert.AreEqual(0, ProcReader.ReadStatusKb(43, procRoot).Count);
				Assert.IsNull(ProcReader.CountOpenFiles(43, procRoot));
			}
			finally
			{
				Directory.Delete(procRoot, true);
			}
		}
	}
}
