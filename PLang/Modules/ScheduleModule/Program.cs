﻿using LightInject;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NCrontab;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.SettingsService;
using PLang.Utils;
using System.ComponentModel;
using System.IO;
using static PLang.Modules.ScheduleModule.Program;

namespace PLang.Modules.ScheduleModule
{
	[Description("Wait, Sleep and time delay. Cron scheduler")]
	public class Program : BaseProgram
	{
		private readonly ISettings settings;
		private readonly ModuleSettings moduleSettings;
		public PrParser PrParser { get; }

		public Program(ISettings settings, PrParser prParser) : base()
		{
			this.settings = settings;
			PrParser = prParser;
			this.moduleSettings = new ModuleSettings(settings);

		}

		[Description("WaitForExecution is always true when calling Sleep")]
		public async Task Sleep(int sleepTimeInMilliseconds)
		{
			//make sure we always wait for execution
			goalStep.WaitForExecution = true;
			await Task.Delay(sleepTimeInMilliseconds);
		}

		public record CronJob(string AbsolutePrFilePath, string CronCommand, string GoalName, Dictionary<string, object?>? Parameters = null, DateTime? NextRun = null, int MaxExecutionTimeInMilliseconds = 30000)
		{
			public bool IsArchived = false;
		};

		[Description("Use numerical representation for cronCommand, e.g. 0 11 * * 1. goalName is the goal that should be called, it should be prefixed by ! and be whole word with possible slash(/).")]
		public async Task Schedule(string cronCommand, string goalName, Dictionary<string, object?>? parameters = null, DateTime? nextRun = null)
		{
			await Schedule(goalStep.AbsolutePrFilePath, cronCommand, goalName, parameters, nextRun);
		}

		private async Task Schedule(string absolutePrFilePath, string cronCommand, string goalName, Dictionary<string, object?>? parameters = null, DateTime? nextRun = null)
		{
			var cronJobs = moduleSettings.GetCronJobs();
			var idx = cronJobs.FindIndex(p => p.AbsolutePrFilePath == absolutePrFilePath);
			if (idx == -1)
			{
				var cronJob = new CronJob(absolutePrFilePath, cronCommand, goalName, parameters, nextRun);
				cronJobs.Add(cronJob);
			}
			else
			{
				cronJobs[idx] = new CronJob(absolutePrFilePath, cronCommand, goalName, parameters, nextRun);
			}

			settings.SetList(typeof(ModuleSettings), cronJobs);

			KeepAlive(this, "Scheduler");
		}

		public static void Start(ISettings settings, IEngine engine, PrParser prParser, ILogger logger, IPseudoRuntime pseudoRuntime, IPLangFileSystem fileSystem)
		{
			RunContainer(settings, engine, prParser, logger, pseudoRuntime, fileSystem);

			var apps = prParser.GetApps();
			foreach (var app in apps)
			{
				var container = new ServiceContainer();
				container.RegisterForPLangConsole(app.AbsoluteAppStartupFolderPath, app.RelativeAppStartupFolderPath);
				var appEngine = container.GetInstance<IEngine>();
				appEngine.Init(container);

				container.RegisterForPLangConsole(app.AbsoluteAppStartupFolderPath, app.RelativeGoalFolderPath);
				RunContainer(container.GetInstance<ISettings>(), appEngine
					, container.GetInstance<PrParser>(), container.GetInstance<ILogger>()
					, container.GetInstance<IPseudoRuntime>(), container.GetInstance<IPLangFileSystem>());

			}


		}

		private static void RunContainer(ISettings settings, IEngine engine, PrParser prParser, ILogger logger, IPseudoRuntime pseudoRuntime, IPLangFileSystem fileSystem)
		{
			var moduleSettings = new ModuleSettings(settings);
			var list = moduleSettings.GetCronJobs();
			list = CleanDeletedCronJobs(settings, fileSystem, list);


			if (list.Count == 0) return;

			Task.Run((Func<Task?>)(async () =>
			{
				await Task.Delay(1000);

				while (true)
				{
					await RunScheduledTasks(settings, engine, prParser, logger, pseudoRuntime, fileSystem);

					//run every 1 min
					await Task.Delay(60 * 1000);
				}
			}));

		}

		private static List<CronJob> CleanDeletedCronJobs(ISettings settings, IPLangFileSystem fileSystem, List<CronJob> cronJobs)
		{
			for (int i = 0; i < cronJobs.Count; i++)
			{
				CronJob cronJob = cronJobs[i];
				if (cronJob.AbsolutePrFilePath == null || !fileSystem.File.Exists(cronJob.AbsolutePrFilePath))
				{
					cronJobs.RemoveAt(i);
					i--;
				}
			}

			settings.SetList(typeof(ModuleSettings), cronJobs);
			return cronJobs;
		}

		public static async Task RunScheduledTasks(ISettings settings, IEngine engine, PrParser prParser, ILogger logger, IPseudoRuntime pseudoRuntime, IPLangFileSystem fileSystem)
		{
			CronJob item = null;
			try
			{
				var list = settings.GetValues<CronJob>(typeof(ModuleSettings));

				for (int i = 0; i < list.Count; i++)
				{
					item = list[i];

					var p = new Program(settings, prParser);
					var schedule = CrontabSchedule.Parse(item.CronCommand);

					var nextOccurrence = schedule.GetNextOccurrence(SystemTime.OffsetUtcNow().DateTime);
					if (item.NextRun == null)
					{
						await p.Schedule(item.AbsolutePrFilePath, item.CronCommand, item.GoalName, item.Parameters, nextOccurrence);
						continue;
					}
					if (item.NextRun > SystemTime.OffsetUtcNow().DateTime)
					{
						continue;
					}

					logger.LogDebug($"Running cron - GoalName:{item.GoalName}");

					using (CancellationTokenSource cts = new CancellationTokenSource())
					{
						int maxExecutionTime = (item.MaxExecutionTimeInMilliseconds == 0) ? 30000 : item.MaxExecutionTimeInMilliseconds;
						cts.CancelAfter(maxExecutionTime);
						var result = await pseudoRuntime.RunGoal(engine, engine.GetContext(), fileSystem.RelativeAppPath, item.GoalName, item.Parameters);
						if (result.error != null)
						{
							logger.LogError(result.error.ToString());
						}
					}

					nextOccurrence = schedule.GetNextOccurrence(SystemTime.OffsetUtcNow().DateTime);
					await p.Schedule(item.AbsolutePrFilePath, item.CronCommand, item.GoalName, item.Parameters, nextOccurrence);

				}
			}
			catch (OperationCanceledException)
			{
				throw new TimeoutException($"{fileSystem.RelativeAppPath} doing cronjob {item!.CronCommand} calling {item!.GoalName} took longer then {item!.MaxExecutionTimeInMilliseconds}ms to run.");
			}
			catch (Exception ex)
			{
				logger.LogError(ex, ex.ToString());
			}
		}
	}
}
