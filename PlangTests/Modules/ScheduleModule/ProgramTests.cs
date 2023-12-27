using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PLang.Modules.ScheduleModule;
using System.Diagnostics;
using NSubstitute;
using static PLang.Modules.ScheduleModule.Program;
using PLang.Interfaces;
using PLang.Utils;
using PLang.Building.Parsers;

namespace PLangTests.Modules.ScheduleModule
{
	[TestClass]
	public class ProgramTests : BasePLangTest
	{
		[TestInitialize]
		public void Init()
		{
			base.Initialize();
		}

		[TestMethod]
		public async Task Sleep_Test()
		{
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			var p = new Program(settings, prParser);
			await p.Sleep(100);
			stopwatch.Stop();
			Assert.IsTrue(stopwatch.ElapsedMilliseconds > 99);
		}

		[TestMethod]
		public async Task CronJob_Test()
		{
			string cronCommand = "* * * * *"; //every 1 min
			string goalName = "Process";

			var cronJobs = new List<CronJob>();
			settings.GetValues<CronJob>(typeof(ModuleSettings)).Returns(cronJobs);
			settings.When(p => p.SetList(typeof(ModuleSettings), Arg.Any<CronJob>()))
				.Do(callback =>
				{
					cronJobs = callback.Arg<List<CronJob>>();
				});


			var p = new Program(settings, prParser);
			await p.Schedule(cronCommand, goalName);

			settings.Received(1).SetList(typeof(ModuleSettings), Arg.Is<List<CronJob>>(list => 
				list.FirstOrDefault().CronCommand == cronCommand && list.FirstOrDefault().GoalName == goalName && list.FirstOrDefault().NextRun == null
			));

		}

		[TestMethod]
		public async Task Start_Test()
		{
			var now = DateTimeOffset.UtcNow;
			string cronCommand = "* * * * *"; //every 1 min
			string goalName = "Process";
			string goalName2 = "Process2";
			var cronJobs = new List<CronJob>();
			cronJobs.Add(new CronJob(cronCommand, goalName, now.AddMinutes(-2).DateTime));
			cronJobs.Add(new CronJob(cronCommand, goalName2));


			settings.GetValues<CronJob>(typeof(ModuleSettings)).Returns(p =>
			{
				return cronJobs;
			});

			settings.When(p => p.SetList(typeof(ModuleSettings), Arg.Any<List<CronJob>>()))
			.Do(callInfo =>
			{				
				cronJobs = callInfo.Arg<List<CronJob>>();
			});



			SystemTime.OffsetUtcNow = () =>
			{
				return now;
			};

			await Program.RunScheduledTasks(settings, engine, prParser, logger, pseudoRuntime, fileSystem);
			await pseudoRuntime.Received(1).RunGoal(engine, Arg.Any<PLangAppContext>(), Arg.Any<string>(), "Process", Arg.Any<Dictionary<string, object>>());


			SystemTime.OffsetUtcNow = () =>
			{
				return now.AddMinutes(1).AddSeconds(1);
			};

			await Program.RunScheduledTasks(settings, engine, prParser, logger, pseudoRuntime, fileSystem);
			await pseudoRuntime.Received(3).RunGoal(engine, Arg.Any<PLangAppContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, object>>());

			SystemTime.OffsetUtcNow = () =>
			{
				return now.AddSeconds(10);
			};

			await Program.RunScheduledTasks(settings, engine, prParser, logger, pseudoRuntime, fileSystem);
			await pseudoRuntime.Received(3).RunGoal(engine, Arg.Any<PLangAppContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, object>>());

			SystemTime.OffsetUtcNow = () =>
			{
				return DateTimeOffset.UtcNow;
			};
		}



		[TestMethod]
		public async Task Start_WithApps_Test()
		{
			var now = DateTimeOffset.UtcNow;
			string cronCommand = "* * * * *"; //every 1 min
			string goalName = "Process";
			string goalName2 = "Process2";
			var cronJobs = new List<CronJob>();
			cronJobs.Add(new CronJob(cronCommand, goalName, now.AddMinutes(-2).DateTime));
			cronJobs.Add(new CronJob(cronCommand, goalName2));


			settings.GetValues<CronJob>(typeof(ModuleSettings)).Returns(p =>
			{
				return cronJobs;
			});

			settings.When(p => p.Set(typeof(ModuleSettings), Arg.Any<string>(), Arg.Any<CronJob>()))
			.Do(callInfo =>
			{

				var cronJob = callInfo.Arg<CronJob>();
				var idx = cronJobs.FindIndex(p => p.CronCommand == cronJob.CronCommand && p.GoalName == cronJob.GoalName);
				cronJobs[idx] = cronJob;

			});

			fileSystem.AddFile(Path.Join("apps", "HelloWorld", "HelloWorld.pr"),
				new System.IO.Abstractions.TestingHelpers.MockFileData(File.ReadAllText(Path.Join("PrFiles", "HelloWorld.pr"))));

			SystemTime.OffsetUtcNow = () =>
			{
				return now;
			};

			await Program.RunScheduledTasks(settings, engine, prParser, logger, pseudoRuntime, fileSystem);
			await pseudoRuntime.Received(1).RunGoal(engine, Arg.Any<PLangAppContext>(), Arg.Any<string>(), "Process", Arg.Any<Dictionary<string, object>>());


		}
	}
}
