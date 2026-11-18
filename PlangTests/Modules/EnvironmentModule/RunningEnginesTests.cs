using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Interfaces;
using PLang.Modules.EnvironmentModule;
using PLang.Runtime;
using PLang.Utils;
using System.Diagnostics;

namespace PLangTests.Modules.EnvironmentModule
{
	[TestClass]
	public class RunningEnginesTests
	{
		private KeepAliveRegistry registry = null!;
		private IEngine root = null!;

		[TestInitialize]
		public void Init()
		{
			registry = new KeepAliveRegistry();
			var prParser = new PrParser(Substitute.For<IPLangFileSystem>(), Substitute.For<ILogger>());
			root = Substitute.For<IEngine>();
			root.Id.Returns("root");
			root.Name.Returns("Root");
			root.ParentEngine.Returns((IEngine?)null);
			root.KeepAliveRegistry.Returns(registry);
			root.PrParser.Returns(prParser);
		}

		private IEngine RentedEngine(string id, string name, GoalStep? rentedFrom = null)
		{
			var fileSystem = Substitute.For<IPLangFileSystem>();
			var logger = Substitute.For<ILogger>();
			var settings = Substitute.For<ISettings>();
			var engine = Substitute.For<IEngine>();
			engine.Id.Returns(id);
			engine.Name.Returns(name);
			engine.ParentEngine.Returns(root);
			engine.KeepAliveRegistry.Returns(registry);
			engine.RentedFrom.Returns(rentedFrom);
			engine.RunningThreadId.Returns(7);

			var runtime = new PseudoRuntime(fileSystem, new PrParser(fileSystem, logger), logger);
			var memoryStack = new MemoryStack(runtime, engine, settings, new VariableHelper(settings, logger), Substitute.For<IPLangContextAccessor>());
			var context = new PLangContext(memoryStack, engine, ExecutionMode.Console);
			engine.RunningContext.Returns(context);
			registry.AddEngine(engine);
			return engine;
		}

		private static GoalStep Step(Goal goal, int line, string text)
		{
			var step = new GoalStep { Text = text, LineNumber = line, RelativeGoalPath = goal.RelativeGoalPath, Goal = goal };
			goal.GoalSteps.Add(step);
			return step;
		}

		[TestMethod]
		public void Read_DescribesEngineWhereItIsAndItsCallStack()
		{
			var start = new Goal { GoalName = "Start", RelativeGoalPath = "/Start.goal" };
			var callStep = Step(start, 3, "call goal Hold, dont wait");
			var engine = RentedEngine("e1", "Child", callStep);

			var hold = new Goal { GoalName = "Hold", RelativeGoalPath = "/Hold.goal" };
			var sleepStep = Step(hold, 5, "wait for 10 sec");
			var callStack = engine.RunningContext!.CallStack;
			callStack.EnterGoal(start);
			callStack.SetCurrentStep(callStep, 0);
			callStack.EnterGoal(hold);
			callStack.SetCurrentStep(Step(hold, 4, "set %x% = 1"), 0);
			callStack.SetCurrentStep(sleepStep, 1);
			engine.RunningContext.MemoryStack.Put("big", "value");

			var result = RunningEnginesReader.Read(root);

			var row = result.Single();
			Assert.AreEqual("e1", row.Id);
			Assert.AreEqual("Child", row.Name);
			Assert.IsFalse(row.IsRoot);
			Assert.AreEqual("running", row.State);
			Assert.AreEqual("/Hold.goal", row.Goal);
			Assert.AreEqual("/Hold.goal:5", row.Step);
			Assert.AreEqual("wait for 10 sec", row.StepText);
			Assert.AreEqual("/Start.goal:3", row.CalledFrom);
			Assert.AreEqual(7, row.ThreadId);
			Assert.AreEqual(engine.RunningContext.MemoryStack.VariableCount, row.MemoryStackVariables);
			Assert.AreEqual(2, row.CallStack.Depth);
			Assert.AreEqual("/Hold.goal", row.CallStack.Frames[0].Goal, "current frame first");
			Assert.AreEqual(2, row.CallStack.Frames[0].ExecutedSteps);
			Assert.AreEqual("/Start.goal:3", row.CallStack.Frames[1].Step);
			Assert.AreEqual(3, row.CallStack.ExecutedSteps);
			Assert.IsNull(row.SerializeBytes, "sizes only on request");

			var json = JsonConvert.SerializeObject(row);
			StringAssert.Contains(json, "\"called_from\":\"/Start.goal:3\"");
			StringAssert.Contains(json, "\"running_seconds\":");
			StringAssert.Contains(json, "\"call_stack\":{\"depth\":2");
			StringAssert.Contains(json, "\"executed_steps\":3");
		}

		[TestMethod]
		public void Read_IdleEngine_AndRootOnlyWhenAsked()
		{
			RentedEngine("e1", "Child");
			root.RunningContext.Returns((PLangContext?)null);

			Assert.AreEqual("idle", RunningEnginesReader.Read(root).Single().State);

			var withRoot = RunningEnginesReader.Read(root, includeRoot: true);
			Assert.AreEqual(2, withRoot.Count);
			Assert.IsTrue(withRoot[0].IsRoot, "the root engine has run since the process started, the longest");
		}

		[TestMethod]
		public void Read_SortsByRunningSeconds_AndTakesTop()
		{
			RentedEngine("oldest", "a");
			Thread.Sleep(20);
			RentedEngine("middle", "b");
			Thread.Sleep(20);
			RentedEngine("newest", "c");

			var all = RunningEnginesReader.Read(root, top: 0);
			CollectionAssert.AreEqual(new[] { "oldest", "middle", "newest" }, all.Select(p => p.Id).ToArray());

			var top2 = RunningEnginesReader.Read(root, top: 2);
			CollectionAssert.AreEqual(new[] { "oldest", "middle" }, top2.Select(p => p.Id).ToArray());
		}

		[TestMethod]
		public void Read_WithSizes_LargestFirstWithItsLargestVariables_SystemVariablesSkipped()
		{
			var small = RentedEngine("small", "Small");
			small.RunningContext!.MemoryStack.Put("name", "plang");
			var large = RentedEngine("large", "Large");
			var memoryStack = large.RunningContext!.MemoryStack;
			memoryStack.Put("items", Enumerable.Range(0, 50_000).Select(i => "item number " + i).ToList());
			memoryStack.Put("label", "x");
			memoryStack.Put("engine", Substitute.For<IEngine>());

			var result = RunningEnginesReader.Read(root, withSizes: true);

			Assert.AreEqual("large", result[0].Id, "sorted by size although it is newer");
			Assert.IsTrue(result[0].SerializeBytes > 50_000 * 12, result[0].SerializeBytes?.ToString());
			Assert.AreEqual("items", result[0].LargestVariables![0].Name);
			Assert.IsFalse(result[0].Capped!.Value);
			Assert.IsFalse(result[0].Truncated!.Value);
			var names = result[0].LargestVariables!.Select(p => p.Name).ToList();
			Assert.IsFalse(names.Contains("engine"), "runtime objects are not measured");
			Assert.IsFalse(names.Contains("Now") || names.Contains(ReservedKeywords.GUID), "system variables are not measured");
			Assert.AreEqual("\"plang\"".Length, result[1].SerializeBytes);

			var json = JsonConvert.SerializeObject(result[0]);
			StringAssert.Contains(json, "\"serialize_bytes\":");
			StringAssert.Contains(json, "\"largest_variables\":[{\"name\":\"items\",\"bytes\":");
		}

		[TestMethod]
		public void MeasureMemoryStack_CapsVariableAndEngine()
		{
			var engine = RentedEngine("e1", "Child");
			var memoryStack = engine.RunningContext!.MemoryStack;
			string twoKb = new string('a', 2000);
			memoryStack.Put("v1", twoKb);
			memoryStack.Put("v2", twoKb);
			memoryStack.Put("v3", twoKb);

			var variableCapped = RunningEnginesReader.MeasureMemoryStack(memoryStack, new EngineSizeLimits(1000, 1_000_000, TimeSpan.FromSeconds(5)), Stopwatch.StartNew());
			Assert.AreEqual(3000, variableCapped.Bytes);
			Assert.IsTrue(variableCapped.Capped);
			Assert.IsTrue(variableCapped.Largest.All(p => p.Capped && p.Bytes == 1000));

			var engineCapped = RunningEnginesReader.MeasureMemoryStack(memoryStack, new EngineSizeLimits(1_000_000, 3000, TimeSpan.FromSeconds(5)), Stopwatch.StartNew());
			Assert.AreEqual(3000, engineCapped.Bytes, "counting stops at the engine cap");
			Assert.IsTrue(engineCapped.Capped);
			Assert.AreEqual(2, engineCapped.Largest.Count, "the variables after the cap are not measured");
		}

		[TestMethod]
		public void Read_WithSizes_TimeLimitTruncates_EnginesKeepTheirOtherFields()
		{
			RentedEngine("e1", "Child").RunningContext!.MemoryStack.Put("name", "plang");
			RentedEngine("e2", "Child").RunningContext!.MemoryStack.Put("name", "plang");

			var result = RunningEnginesReader.Read(root, withSizes: true, limits: new EngineSizeLimits(1000, 1000, TimeSpan.Zero));

			Assert.AreEqual(2, result.Count);
			Assert.IsTrue(result.All(p => p.Truncated == true && p.SerializeBytes == null && p.Id != null));
		}

		private class Throwing
		{
			public string Name => "ok";
			public string Broken => throw new InvalidOperationException("cannot read");
		}

		[TestMethod]
		public void MeasureMemoryStack_ObjectsThatFailOrChange_DoNotThrow()
		{
			var engine = RentedEngine("e1", "Child");
			var memoryStack = engine.RunningContext!.MemoryStack;
			memoryStack.Put("throwing", new Throwing());
			var list = Enumerable.Range(0, 1000).Select(i => "item" + i).ToList();
			memoryStack.Put("changing", list);

			using var stop = new CancellationTokenSource();
			var writer = Task.Run(() =>
			{
				while (!stop.IsCancellationRequested)
				{
					list.Add("more");
					if (list.Count > 5000) list.RemoveRange(1000, 4000);
				}
			});
			try
			{
				for (int i = 0; i < 20; i++)
				{
					var size = RunningEnginesReader.MeasureMemoryStack(memoryStack, EngineSizeLimits.Default, Stopwatch.StartNew());
					Assert.IsTrue(size.Largest.Any(p => p.Name == "throwing" && p.Bytes > 0));
				}
			}
			finally
			{
				stop.Cancel();
				writer.Wait();
			}
		}

		[TestMethod]
		public void RuntimeStats_EnginesRunning_Oldest_AndLargestOnlyWhenMeasured()
		{
			var engine = RentedEngine("e1", "Child");
			engine.RunningContext!.MemoryStack.Put("name", "plang");

			var before = RuntimeStatsReader.Read(root, null);
			Assert.AreEqual(1, before.Plang.EnginesRunning);
			Assert.AreEqual(0, before.Plang.KeepAliveEntries, "a rented engine does not keep the app alive");
			Assert.IsFalse(before.Plang.RunningTasks.ContainsKey(KeepAliveRegistry.Engine));
			Assert.IsNull(before.Plang.LargestEngine, "get runtime stats never measures");

			RunningEnginesReader.Read(root, withSizes: true);
			var after = RuntimeStatsReader.Read(root, null);
			Assert.AreEqual("e1", after.Plang.LargestEngine!.Id);
			Assert.AreEqual("\"plang\"".Length, after.Plang.LargestEngine.SerializeBytes);
			StringAssert.Contains(JsonConvert.SerializeObject(after), "\"engines_running\":1");

			registry.RemoveEngine(engine);
			var returned = RuntimeStatsReader.Read(root, null);
			Assert.AreEqual(0, returned.Plang.EnginesRunning);
			Assert.AreEqual(0, returned.Plang.OldestRunningEngineSeconds);
			Assert.IsNull(returned.Plang.LargestEngine);
		}
	}
}
