using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Modules.EnvironmentModule;
using PLang.Runtime;
using PLang.Services.SettingsService;

namespace PLangTests.Modules.EnvironmentModule
{
	[TestClass]
	public class RunningTasksTests
	{
		[TestMethod]
		public async Task GetRunningTasks_ListsGoalTasksAndListeners()
		{
			var registry = new KeepAliveRegistry();
			var engine = Substitute.For<IEngine>();
			engine.KeepAliveRegistry.Returns(registry);

			var running = new TaskCompletionSource<(object? Variables, IError? Error)>().Task;
			var goal = new Goal { GoalName = "Loop", RelativeGoalPath = "/Loop.goal" };
			var caller = new Goal { GoalName = "Start", RelativeGoalPath = "/Start.goal" };
			registry.Add(KeepAliveRegistry.DontWait, "Loop", new PlangTask(running, goal, caller));
			registry.Add(KeepAliveRegistry.Schedule, "Tick", new PlangTask(running, new Goal { GoalName = "Tick", RelativeGoalPath = "/Tick.goal" }, null));
			registry.Add("Webserver", "default *:8080", new object());

			var program = new Program(Substitute.For<ISettings>(), Substitute.For<IPLangFileSystem>(), Substitute.For<ISettingsRepositoryFactory>(), engine);
			var tasks = await program.GetRunningTasks();

			Assert.AreEqual(3, tasks.Count);
			Assert.AreEqual("WaitForExecution", tasks[0].Kind);
			Assert.AreEqual("/Loop.goal", tasks[0].Goal);
			Assert.AreEqual("/Start.goal", tasks[0].CalledFrom);
			Assert.AreEqual("Schedule", tasks[1].Kind);
			Assert.AreEqual("/Tick.goal", tasks[1].Goal);
			Assert.IsNull(tasks[1].CalledFrom);
			Assert.AreEqual("Webserver", tasks[2].Kind);
			Assert.AreEqual("default *:8080", tasks[2].Name);
			Assert.IsNull(tasks[2].Goal);
			Assert.IsTrue(tasks.All(t => t.RunningSeconds >= 0));

			var json = JsonConvert.SerializeObject(tasks[0]);
			StringAssert.Contains(json, "\"called_from\":\"/Start.goal\"");
			StringAssert.Contains(json, "\"running_seconds\"");
		}
	}
}
