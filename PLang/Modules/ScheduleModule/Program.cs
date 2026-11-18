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
using PLang.Events;
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
	[Description("Wait, Sleep and time delay. Schedule a goal to be called repeatedly ('every 15 seconds', 'every 5 minutes', 'every day at 8am'), cron scheduler")]
	[Effect(Effects.None)]
	public class Program : BaseProgram
	{
		private readonly IPseudoRuntime pseudoRuntime;
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
		public async Task<IError?> WaitIncreasingly(string key, List<int> millisecondsDelay, int timeoutInSeconds = 5 * 60)
		{
			if (string.IsNullOrEmpty(key)) return new ProgramError("Key cannot be empty");

			string cacheKey = "WaitIncreasingly_" + key;
			var request = (await appCache.Get(cacheKey)) as WaitIncreasignlyCounter;
			if (request == null) request = new WaitIncreasignlyCounter(key, 0);

			int waitFor = 0;
			if (millisecondsDelay.Count <= request.Counter)
			{
				waitFor = millisecondsDelay.LastOrDefault();
			}
			else
			{
				waitFor = millisecondsDelay[request.Counter];
			}
			request = request with { Counter = request.Counter + 1 };

			await appCache.Set(cacheKey, request, TimeSpan.FromSeconds(timeoutInSeconds));

			//make sure we always wait for execution
			goalStep.WaitForExecution = true;

			if (waitFor == 0) return null;

			logger.LogWarning($"Waiting {waitFor} (counter:{request.Counter}) because of key:{key} - ");
			await Task.Delay(waitFor);


			return null;
		}

		[Description(@"Blocks the step until the existing variable %variableName% has not been changed for timeInMilliseconds, then calls goalToCall once (debounce). Only use when the user names a variable to wait on, e.g. 'wait until %searchText% has not changed for 500ms, call Search'.
NEVER use for a recurring call such as 'every 15 seconds call X' or 'every 5 minutes, call X'; use Schedule for those.")]
		public async Task<IError?> WaitOnVariable([HandlesVariable] string variableName, GoalToCallInfo goalToCall, long timeInMilliseconds = 1000)
		{
			if (timeInMilliseconds == 0)
			{
				return new ProgramError("You must define the time period to wait on a variable", goalStep, function);
			}

			// A variable that does not exist is a fresh object on every read, so it would never settle and the step would wait forever.
			if (!memoryStack.GetObjectValue(variableName, false).Initiated)
			{
				return new ProgramError($"Variable {variableName} does not exist, there is nothing to wait on", goalStep, function,
					FixSuggestion: "WaitOnVariable waits for a variable to stop changing. To call a goal repeatedly, write e.g. 'every 15 seconds, call X' so it builds as Schedule");
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

		[Description(@"Calls goalName repeatedly, on every 'every ...' / 'each ...' / cron statement, also for intervals in seconds. The step registers the job and returns at once; it does not block the steps after it.
cronCommand is a cron expression, either 5 fields (minute hour day-of-month month day-of-week) or, only when the interval is in seconds, 6 fields with seconds first (second minute hour day-of-month month day-of-week). Map the natural-language interval to the cron fields exactly; do NOT collapse an interval to a single daily run. Examples:
'every 5 seconds, call X' => '*/5 * * * * *'
'every 15 seconds call goal X' => '*/15 * * * * *'
'every 30 seconds' => '*/30 * * * * *'
'every minute' => '* * * * *'
'every 5 minutes, call X' => '*/5 * * * *'
'every 10 minutes' => '*/10 * * * *'
'every 1 hour' / 'every hour' / 'hourly' => '0 * * * *'
'every 2 hours' => '0 */2 * * *'
'every day at 8am' => '0 8 * * *'
'every day at 7:30am' => '30 7 * * *'
'at 11:00 on Monday' => '0 11 * * 1'
'on 5th of every month at 7am' => '0 7 5 * *'.
goalName is the goal that should be called, a whole word with possible slash(/), written without a prefix.")]
		public async Task<IError?> Schedule(string cronCommand, GoalToCallInfo goalName, DateTime? nextRun = null)
		{
			try
			{
				ParseCron(cronCommand);
			}
			catch (CrontabException ex)
			{
				return new ProgramError($"'{cronCommand}' is not a valid cron expression: {ex.Message}", goalStep, function, Exception: ex,
					FixSuggestion: "Use 5 fields (minute hour day-of-month month day-of-week), e.g. '*/5 * * * *' for every 5 minutes, or 6 fields with seconds first, e.g. '*/15 * * * * *' for every 15 seconds");
			}

			SaveCronJob(settings, goalStep.AbsolutePrFilePath, cronCommand, goalName, nextRun);
			Interlocked.Increment(ref cronJobsVersion);
			return null;
		}

		// Incremented when a step writes a cron job, so a running scheduler loop reads the job list again on its next tick.
		private static long cronJobsVersion;

		// The scheduler loop also reads the job list at this interval, to pick up jobs changed outside this process and to drop jobs whose .pr file is gone.
		private static readonly TimeSpan CronJobsReloadInterval = TimeSpan.FromSeconds(60);

		/// <summary>
		/// Parses a 5-field cron expression (minute precision) or a 6-field one whose first field is seconds.
		/// </summary>
		public static CrontabSchedule ParseCron(string cronCommand)
		{
			var fieldCount = (cronCommand ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
			return CrontabSchedule.Parse(cronCommand, new CrontabSchedule.ParseOptions { IncludingSeconds = fieldCount == 6 });
		}

		/// <summary>
		/// The first occurrence of cronCommand strictly after from.
		/// </summary>
		public static DateTime GetNextRun(string cronCommand, DateTime from)
		{
			return ParseCron(cronCommand).GetNextOccurrence(from);
		}

		/// <summary>
		/// Jobs whose NextRun has been reached at now, in list order. A job without NextRun has not been given its first run time yet and is not due.
		/// </summary>
		public static List<CronJob> GetDueJobs(IEnumerable<CronJob> cronJobs, DateTime now)
		{
			return cronJobs.Where(p => p.NextRun != null && p.NextRun <= now).ToList();
		}

		private static void SaveCronJob(ISettings settings, string absolutePrFilePath, string cronCommand, GoalToCallInfo goalName, DateTime? nextRun)
		{
			var cronJobs = new ModuleSettings(settings).GetCronJobs();
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
				var containerForScheduler = new ServiceContainer();


				containerForScheduler.RegisterForPLang(fileSystem.GoalsPath, fileSystem.Path.DirectorySeparatorChar.ToString(), null, null, parentEngine: engine);

				engine = containerForScheduler.GetInstance<IEngine>();
				engine.Init(containerForScheduler);
				settings = containerForScheduler.GetInstance<ISettings>();
				prParser = containerForScheduler.GetInstance<PrParser>();
				logger = containerForScheduler.GetInstance<ILogger>();
				pseudoRuntime = containerForScheduler.GetInstance<IPseudoRuntime>();
				fileSystem = containerForScheduler.GetInstance<IPLangFileSystem>();
				engine.Name = "Scheduler";
				engine.Environment = this.engine.Environment;
				Start(settings, engine, prParser, logger, pseudoRuntime, fileSystem);


				KeepAlive(containerForScheduler, "Scheduler");

			}
			else
			{


				Start(settings, engine, prParser, logger, pseudoRuntime, fileSystem);


				KeepAlive(engine, "Scheduler");
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
			// The loop ticks every second. A tick only compares the in-memory NextRun times with the clock; the job list is read
			// from settings when a job is due, when a step has written a job, or every CronJobsReloadInterval.
			Task.Run((Func<Task?>)(async () =>
			{
				await Task.Delay(1000);

				List<CronJob> cronJobs = new();
				long loadedVersion = -1;
				DateTime lastLoad = DateTime.MinValue;

				while (true)
				{
					try
					{
						var now = SystemTime.OffsetUtcNow().DateTime;
						var version = Interlocked.Read(ref cronJobsVersion);
						bool hasDueJob = GetDueJobs(cronJobs, now).Count > 0;

						if (hasDueJob || version != loadedVersion || now - lastLoad >= CronJobsReloadInterval)
						{
							loadedVersion = version;
							lastLoad = now;
							cronJobs = LoadCronJobs(settings, fileSystem, logger, now);
							cronJobs = await RunScheduledTasks(cronJobs, settings, engine, logger, pseudoRuntime, fileSystem);
						}
					}
					catch (Exception ex)
					{
						logger.LogError(ex, ex.ToString());
					}

					// wake up just after the next whole second, the finest unit a cron expression has
					var millisecond = SystemTime.OffsetUtcNow().Millisecond;
					await Task.Delay(1000 - millisecond + 10);
				}
			}));

		}

		/// <summary>
		/// Reads the active cron jobs, drops jobs whose .pr file is gone and gives a job without NextRun its first run time.
		/// Jobs with an invalid cron expression are logged and left out.
		/// </summary>
		private List<CronJob> LoadCronJobs(ISettings settings, IPLangFileSystem fileSystem, ILogger logger, DateTime now)
		{
			var list = CleanDeletedCronJobs(settings, fileSystem, new ModuleSettings(settings).GetCronJobs());

			var cronJobs = new List<CronJob>();
			foreach (var item in list)
			{
				var cronJob = item;
				if (cronJob.NextRun == null)
				{
					DateTime nextRun;
					try
					{
						nextRun = GetNextRun(cronJob.CronCommand, now);
					}
					catch (CrontabException ex)
					{
						logger.LogError($"Cron job '{cronJob.CronCommand}' calling {cronJob.GoalName} in {cronJob.AbsolutePrFilePath} is not a valid cron expression: {ex.Message}");
						continue;
					}
					SaveCronJob(settings, cronJob.AbsolutePrFilePath, cronJob.CronCommand, cronJob.GoalName, nextRun);
					cronJob = cronJob with { NextRun = nextRun };
				}
				cronJobs.Add(cronJob);
			}
			return cronJobs;
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

			if (counter > 0)
			{
				settings.SetList(typeof(ModuleSettings), cronJobs);
			}
			return cronJobs;
		}


		/// <summary>
		/// Runs the due jobs in cronJobs one after another and stores each one's next run time.
		/// Returns cronJobs with the new NextRun values.
		/// </summary>
		private async Task<List<CronJob>> RunScheduledTasks(List<CronJob> cronJobs, ISettings settings, IEngine engine, ILogger logger, IPseudoRuntime pseudoRuntime, IPLangFileSystem fileSystem)
		{
			var dueJobs = GetDueJobs(cronJobs, SystemTime.OffsetUtcNow().DateTime);
			foreach (var item in dueJobs)
			{
				try
				{
					logger.LogInformation($"Running cron - GoalName:{item.GoalName}");

					using (CancellationTokenSource cts = new CancellationTokenSource())
					{
						context.MemoryStack.Clear();

						int maxExecutionTime = (item.MaxExecutionTimeInMilliseconds == 0) ? 30000 : item.MaxExecutionTimeInMilliseconds;
						cts.CancelAfter(maxExecutionTime);
						var (_, _, error) = await pseudoRuntime.RunGoal(engine, contextAccessor, fileSystem.RelativeAppPath, item.GoalName, goal);
						if (error != null)
						{
							if (error != null && error is not EndGoal)
							{
								try
								{
									(_, error) = await engine.GetEventRuntime().AppErrorEvents(error);

									if (error != null)
									{
										Console.WriteLine("Error running async goal:" + error.ToString());

									}
								} catch (Exception ex)
								{
									Console.WriteLine($"GoalName: {item.GoalName}" + ex.ToString());
								}

							}
						}
					}
				}
				catch (OperationCanceledException)
				{
					logger.LogError($"{fileSystem.RelativeAppPath} doing cronjob {item.CronCommand} calling {item.GoalName} took longer then {item.MaxExecutionTimeInMilliseconds}ms to run.");
				}
				catch (Exception ex)
				{
					logger.LogError(ex, ex.ToString());
				}
				finally
				{
					// The next run counts from when this run ended, so a run that takes longer than the interval skips the occurrences it overlapped.
					var nextRun = GetNextRun(item.CronCommand, SystemTime.OffsetUtcNow().DateTime);
					SaveCronJob(settings, item.AbsolutePrFilePath, item.CronCommand, item.GoalName, nextRun);

					var idx = cronJobs.IndexOf(item);
					if (idx != -1) cronJobs[idx] = item with { NextRun = nextRun };
				}
			}
			return cronJobs;
		}
	}
}
