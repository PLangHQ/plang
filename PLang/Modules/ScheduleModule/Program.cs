using LightInject;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NCrontab;
using Newtonsoft.Json;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Services.OutputStream.Sinks;
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
		private readonly IEngine engine;
		private readonly IPseudoRuntime pseudoRuntime;
		private readonly ILogger logger;
		private readonly IPLangFileSystem fileSystem;
		private readonly IAppCache appCache;
		private readonly ModuleSettings moduleSettings;
		public PrParser PrParser { get; }

		public Program(ISettings settings, PrParser prParser, IEngine engine, IPseudoRuntime pseudoRuntime, 
			ILogger logger, IPLangFileSystem fileSystem, IAppCache appCache) : base()
		{
			this.settings = settings;
			PrParser = prParser;
			this.engine = engine;
			this.pseudoRuntime = pseudoRuntime;
			this.logger = logger;
			this.fileSystem = fileSystem;
			this.appCache = appCache;
			this.moduleSettings = new ModuleSettings(settings);

		}

		[Description("WaitForExecution is always true when calling Sleep")]
		[MethodSettings(CanBeAsync = false)]
		public async Task Sleep(int sleepTimeInMilliseconds)
		{
			//make sure we always wait for execution
			goalStep.WaitForExecution = true;
			await Task.Delay(sleepTimeInMilliseconds);
		}

		public record WaitIncreasignlyCounter(string Key, int Counter);
		[Description("Waits increasingly in a key")]
		[MethodSettings(CanBeAsync = false)]
		public async Task<IError?> WaitIncreasingly(string key, List<int> millisecondsDelay, int timeoutInSeconds = 5*60)
		{
			if (string.IsNullOrEmpty(key)) return new ProgramError("Key cannot be empty");

			string cacheKey = "WaitIncreasingly_" + key;
			var request = (await appCache.Get(cacheKey)) as WaitIncreasignlyCounter;
			if (request == null) request = new WaitIncreasignlyCounter(key, 0);

			int waitFor = 0;
			if (millisecondsDelay.Count <= request.Counter)
			{
				waitFor = millisecondsDelay.LastOrDefault();
			} else
			{
				waitFor = millisecondsDelay[request.Counter];
			}
			request = request with { Counter = request.Counter+1 };

			await appCache.Set(cacheKey, request, TimeSpan.FromSeconds(timeoutInSeconds));
			
			//make sure we always wait for execution
			goalStep.WaitForExecution = true;

			if (waitFor == 0) return null;
			
			logger.LogWarning($"Waiting {waitFor} (counter:{request.Counter}) because of key:{key} - ");
			await Task.Delay(waitFor);
			
			
			return null;
		}

		public async Task<IError?> WaitOnVariable([HandlesVariable] string variableName, GoalToCallInfo goalToCall, long timeInMilliseconds = 1000)
		{
			if (timeInMilliseconds == 0)
			{
				return new ProgramError("You must define the time period to wait on a variable", goalStep, function);
			}


			bool hasChanged = true;
			while (hasChanged)
			{
				var obj = memoryStack.GetObjectValue(variableName, false);
				var totalMilliseconds = new TimeSpan(DateTime.Now.Ticks - obj.Updated.Ticks).TotalMilliseconds;
				if (totalMilliseconds > timeInMilliseconds)
				{
					hasChanged = false;
				}

				await Task.Delay(100);
			}
			
			var result = await pseudoRuntime.RunGoal(engine, contextAccessor, fileSystem.RelativeAppPath, goalToCall);

			return result.Error;
		}

		public record CronJob(string AbsolutePrFilePath, string CronCommand, GoalToCallInfo GoalName, DateTime? NextRun = null, int MaxExecutionTimeInMilliseconds = 30000)
		{
			public bool IsArchived = false;
		};

		[Description("Use numerical representation for cronCommand, e.g. 0 11 * * 1. goalName is the goal that should be called, it should be prefixed by ! and be whole word with possible slash(/).")]
		public async Task Schedule(string cronCommand, GoalToCallInfo goalName, DateTime? nextRun = null)
		{
			await Schedule(goalStep.AbsolutePrFilePath, cronCommand, goalName, nextRun);
		}

		private async Task Schedule(string absolutePrFilePath, string cronCommand, GoalToCallInfo goalName, DateTime? nextRun = null)
		{
			var cronJobs = moduleSettings.GetCronJobs();
			var idx = cronJobs.FindIndex(p => p.AbsolutePrFilePath == absolutePrFilePath);
			if (idx == -1)
			{
				var cronJob = new CronJob(absolutePrFilePath, cronCommand, goalName, nextRun);
				cronJobs.Add(cronJob);
			}
			else
			{
				cronJobs[idx] = new CronJob(absolutePrFilePath, cronCommand, goalName, nextRun);
			}

			settings.SetList(typeof(ModuleSettings), cronJobs);

			KeepAlive(this, "Scheduler");
		}

		public async Task StartScheduler()
		{


			IEngine engine = this.engine;
			ISettings settings = this.settings;
			PrParser prParser = this.PrParser;
			ILogger logger = this.logger;
			IPseudoRuntime pseudoRuntime = this.pseudoRuntime;
			IPLangFileSystem fileSystem = this.fileSystem;

			if (context.SystemSink is not AppOutputSink)
			{
				logger.LogDebug("Initiate new engine for scheduler");
				using (var containerForScheduler = new ServiceContainer())
				{
					containerForScheduler.RegisterForPLang(fileSystem.GoalsPath, fileSystem.Path.DirectorySeparatorChar.ToString(), null, null, parentEngine: engine);

					engine = containerForScheduler.GetInstance<IEngine>();
					engine.Init(containerForScheduler);
					settings = containerForScheduler.GetInstance<ISettings>();
					prParser = containerForScheduler.GetInstance<PrParser>();
					logger = containerForScheduler.GetInstance<ILogger>();
					pseudoRuntime = containerForScheduler.GetInstance<IPseudoRuntime>();
					fileSystem = containerForScheduler.GetInstance<IPLangFileSystem>();

					Start(settings, engine, prParser, logger, pseudoRuntime, fileSystem);
				}
			}
			else
			{


				Start(settings, engine, prParser, logger, pseudoRuntime, fileSystem);
			}
			
		}


		private void Start(ISettings settings, IEngine engine, PrParser prParser, ILogger logger, IPseudoRuntime pseudoRuntime, IPLangFileSystem fileSystem)
		{
			RunContainer(settings, engine, prParser, logger, pseudoRuntime, fileSystem);

			/*
			 * This causes scheduled tasks in app to run (and not working 100% correct)
			 * I dont think it should start them for security reasons
			 *
			 
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
			*/

		}

		private void RunContainer(ISettings settings, IEngine engine, PrParser prParser, ILogger logger, IPseudoRuntime pseudoRuntime, IPLangFileSystem fileSystem)
		{
			var moduleSettings = new ModuleSettings(settings);
			var list = moduleSettings.GetCronJobs();
			list = CleanDeletedCronJobs(settings, fileSystem, list);

			logger.LogDebug($"{list.Count} cron jobs to run: " + JsonConvert.SerializeObject(list));
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

		private List<CronJob> CleanDeletedCronJobs(ISettings settings, IPLangFileSystem fileSystem, List<CronJob> cronJobs)
		{
			int counter = 0;
			for (int i = 0; i < cronJobs.Count; i++)
			{
				CronJob cronJob = cronJobs[i];
				if (cronJob.AbsolutePrFilePath == null || !fileSystem.File.Exists(cronJob.AbsolutePrFilePath))
				{
					cronJobs.RemoveAt(i);
					i--;
					counter++;
					if (counter > 100)
					{
						Console.WriteLine("!!! LOOP CleanDeletedCronJobs");
					}
				}
			}

			settings.SetList(typeof(ModuleSettings), cronJobs);
			return cronJobs;
		}


		private async Task RunScheduledTasks(ISettings settings, IEngine engine, PrParser prParser, ILogger logger, IPseudoRuntime pseudoRuntime, IPLangFileSystem fileSystem)
		{
			logger.LogDebug("Running 1 min cron check");
			CronJob? item = null;
			try
			{
				var list = settings.GetValues<CronJob>(typeof(ModuleSettings)).Where(p => !p.IsArchived).ToList();

				for (int i = 0; i < list.Count; i++)
				{
					item = list[i];
					
					var p = new Program(settings, prParser, engine, pseudoRuntime, logger, fileSystem, appCache);
					var schedule = CrontabSchedule.Parse(item.CronCommand);

					var nextOccurrence = schedule.GetNextOccurrence(SystemTime.OffsetUtcNow().DateTime);
					if (item.NextRun == null)
					{
						await p.Schedule(item.AbsolutePrFilePath, item.CronCommand, item.GoalName, nextOccurrence);
						continue;
					}
					if (item.NextRun > SystemTime.OffsetUtcNow().DateTime)
					{
						continue;
					}

					logger.LogInformation($"Running cron - GoalName:{item.GoalName}");

					using (CancellationTokenSource cts = new CancellationTokenSource())
					{
						context.MemoryStack.Clear();

						int maxExecutionTime = (item.MaxExecutionTimeInMilliseconds == 0) ? 30000 : item.MaxExecutionTimeInMilliseconds;
						cts.CancelAfter(maxExecutionTime);
						var result = await pseudoRuntime.RunGoal(engine, contextAccessor, fileSystem.RelativeAppPath, item.GoalName, goal);
						if (result.Error != null)
						{
							logger.LogError(result.Error.ToString());
						}
					}

					nextOccurrence = schedule.GetNextOccurrence(SystemTime.OffsetUtcNow().DateTime);
					await p.Schedule(item.AbsolutePrFilePath, item.CronCommand, item.GoalName, nextOccurrence);

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
