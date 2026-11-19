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
	[Description("Wait, Sleep and time delay. Schedule a goal to be called repeatedly ('every 45 seconds', 'every 5 minutes', 'every day at 8am'), interval and cron scheduler")]
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

		/// <summary>
		/// A scheduled call of GoalName. It runs either every IntervalInSeconds, counted from when the job was scheduled,
		/// or on the clock times of the cron expression CronCommand (evaluated in UTC). A job stored without IntervalInSeconds is a cron job.
		/// </summary>
		public record CronJob(string AbsolutePrFilePath, string? CronCommand, GoalToCallInfo GoalName, DateTime? NextRun = null, long? IntervalInSeconds = null)
		{
			public bool IsArchived = false;

			[JsonIgnore]
			public bool IsInterval => IntervalInSeconds is > 0;

			[JsonIgnore]
			public string Schedule => IsInterval ? $"every {IntervalInSeconds} seconds" : $"cron '{CronCommand}'";
		};

		[Description(@"Calls goalName repeatedly, on every 'every ...' / 'each ...' / cron statement. The step registers the job and returns at once; it does not block the steps after it.
Set exactly one of intervalInSeconds or cronCommand.
intervalInSeconds: when the statement is 'every N seconds/minutes/hours/days' and names no clock time, weekday or day of month. Convert N to seconds. The goal runs every N seconds counted from when it was scheduled, not aligned to the clock. Examples:
'every 5 seconds, call X' => 5
'every 45 seconds call goal X' => 45
'every minute' => 60
'every 5 minutes, call X' => 300
'every 90 minutes' => 5400
'every hour' / 'hourly' => 3600
'every 2 hours' => 7200
'every day' / 'daily' => 86400
cronCommand: when the statement names a clock time, a weekday, a day of the month or a minute of the hour. A cron expression of 5 fields (minute hour day-of-month month day-of-week), or 6 fields with seconds first (second minute hour day-of-month month day-of-week) only when a second is named. Evaluated in UTC. Examples:
'every day at 8am' => '0 8 * * *'
'every day at 7:30am' => '30 7 * * *'
'at 11:00 on Monday' / 'on mondays at 11 am' => '0 11 * * 1'
'every weekday at 6pm' => '0 18 * * 1-5'
'at minute 0 of every hour' / 'on the hour' => '0 * * * *'
'at 15 and 45 minutes past every hour' => '15,45 * * * *'
'on 5th of every month at 7am' => '0 7 5 * *'.
goalName is the goal that should be called, a whole word with possible slash(/), written without a prefix.")]
		public async Task<IError?> Schedule(GoalToCallInfo goalName, string? cronCommand = null, long? intervalInSeconds = null, DateTime? nextRun = null)
		{
			var fixSuggestion = "Use intervalInSeconds for 'every N seconds/minutes/hours/days', e.g. 45 for every 45 seconds, or a cronCommand for clock times, e.g. '0 8 * * *' for every day at 8am";
			if (intervalInSeconds != null)
			{
				if (!string.IsNullOrWhiteSpace(cronCommand))
				{
					return new ProgramError($"Schedule has both an interval ({intervalInSeconds} seconds) and a cron expression ('{cronCommand}'), it can only have one", goalStep, function, FixSuggestion: fixSuggestion);
				}
				if (intervalInSeconds <= 0)
				{
					return new ProgramError($"Interval must be at least 1 second, it is {intervalInSeconds}", goalStep, function, FixSuggestion: fixSuggestion);
				}
				SaveCronJob(settings, KeepNextRun(new ModuleSettings(settings).GetCronJobs(), new CronJob(goalStep.AbsolutePrFilePath, null, goalName, nextRun, intervalInSeconds)));
				Interlocked.Increment(ref cronJobsVersion);
				return null;
			}

			if (string.IsNullOrWhiteSpace(cronCommand))
			{
				return new ProgramError("Schedule needs an interval or a cron expression", goalStep, function, FixSuggestion: fixSuggestion);
			}

			try
			{
				ParseCron(cronCommand);
			}
			catch (CrontabException ex)
			{
				return new ProgramError($"'{cronCommand}' is not a valid cron expression: {ex.Message}", goalStep, function, Exception: ex,
					FixSuggestion: "Use 5 fields (minute hour day-of-month month day-of-week), e.g. '0 8 * * *' for every day at 8am, or 6 fields with seconds first, e.g. '30 0 8 * * *' for 08:00:30. " + fixSuggestion);
			}

			SaveCronJob(settings, KeepNextRun(new ModuleSettings(settings).GetCronJobs(), new CronJob(goalStep.AbsolutePrFilePath, cronCommand, goalName, nextRun)));
			Interlocked.Increment(ref cronJobsVersion);
			return null;
		}

		/// <summary>
		/// The schedule step runs again at every start of the app. A job that is already stored with the same schedule and goal keeps its
		/// stored NextRun, so a restart does not move the next run to one interval after the start, and a run that came due while the app
		/// was down runs once, when the scheduler starts. A new or changed schedule, or a NextRun given by the step, starts over.
		/// </summary>
		public static CronJob KeepNextRun(IEnumerable<CronJob> storedJobs, CronJob created)
		{
			if (created.NextRun != null) return created;

			var stored = storedJobs.FirstOrDefault(p => p.AbsolutePrFilePath == created.AbsolutePrFilePath);
			if (stored == null || stored.NextRun == null) return created;

			bool sameSchedule = stored.IntervalInSeconds == created.IntervalInSeconds
				&& string.Equals(stored.CronCommand, created.CronCommand, StringComparison.Ordinal)
				&& string.Equals(stored.GoalName?.Name, created.GoalName?.Name, StringComparison.OrdinalIgnoreCase);
			return sameSchedule ? created with { NextRun = stored.NextRun } : created;
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
		/// The first run of an interval after scheduledRun that lies after now. Runs stay on the grid scheduledRun + k * interval,
		/// so the period does not drift, and periods that passed while the process was busy or down are dropped instead of run one after another.
		/// </summary>
		public static DateTime GetNextIntervalRun(DateTime scheduledRun, TimeSpan interval, DateTime now)
		{
			var nextRun = scheduledRun + interval;
			if (nextRun > now) return nextRun;

			long missedPeriods = (now - nextRun).Ticks / interval.Ticks + 1;
			return nextRun + TimeSpan.FromTicks(missedPeriods * interval.Ticks);
		}

		/// <summary>
		/// The run of cronJob that follows now. An interval job without NextRun runs one interval after now; one with NextRun keeps
		/// to the grid NextRun + k * interval. A cron job runs at the first occurrence of its expression after now.
		/// </summary>
		public static DateTime GetNextRun(CronJob cronJob, DateTime now)
		{
			if (cronJob.IsInterval)
			{
				var interval = TimeSpan.FromSeconds(cronJob.IntervalInSeconds!.Value);
				return (cronJob.NextRun == null) ? now + interval : GetNextIntervalRun(cronJob.NextRun.Value, interval, now);
			}
			return GetNextRun(cronJob.CronCommand!, now);
		}

		/// <summary>
		/// Jobs whose NextRun has been reached at now, in list order. A job without NextRun has not been given its first run time yet and is not due.
		/// </summary>
		public static List<CronJob> GetDueJobs(IEnumerable<CronJob> cronJobs, DateTime now)
		{
			return cronJobs.Where(p => p.NextRun != null && p.NextRun <= now).ToList();
		}

		/// <summary>
		/// What the scheduler does with a due job at now: start it, or skip this occurrence when its previous run is still going.
		/// NextRun is the run that follows now in both cases.
		/// </summary>
		public record DueJob(CronJob Job, bool Skip, DateTime NextRun);

		public static List<DueJob> PlanDueJobs(IEnumerable<CronJob> cronJobs, DateTime now, Func<CronJob, bool> isRunning)
		{
			return GetDueJobs(cronJobs, now).Select(job => new DueJob(job, isRunning(job), GetNextRun(job, now))).ToList();
		}

		private static void SaveCronJob(ISettings settings, CronJob cronJob)
		{
			var cronJobs = new ModuleSettings(settings).GetCronJobs();
			var idx = cronJobs.FindIndex(p => p.AbsolutePrFilePath == cronJob.AbsolutePrFilePath);
			if (idx == -1)
			{
				cronJobs.Add(cronJob);
			}
			else
			{
				cronJobs[idx] = cronJob;
			}

			settings.SetList(typeof(ModuleSettings), cronJobs);

		}

		[Description("Starts the scheduler. Does nothing when the app was started with --no-scheduler or --standby, unless force is true. e.g. `start scheduler`, `start scheduler, force`")]
		[Example("start scheduler", "force=false")]
		[Example("start scheduler, force", "force=true")]
		public async Task StartScheduler(bool force = false)
		{
			if (!RuntimeMode.SchedulerEnabled)
			{
				if (!force)
				{
					this.logger.LogInformation("Scheduler not started, the app was started with --no-scheduler or --standby. Use `enable scheduler` to start it");
					return;
				}
				RuntimeMode.SetSchedulerEnabled(true);
			}

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


		[Description("Clears --no-scheduler/--standby and starts the scheduler. e.g. `enable scheduler`")]
		[Example("enable scheduler", "")]
		public async Task EnableScheduler()
		{
			await StartScheduler(force: true);
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
			// Due jobs are started without waiting for them, so the loop keeps its tick while jobs run, and runningJobs
			// (only touched by this loop) holds each job's run until it has finished.
			Task.Run((Func<Task?>)(async () =>
			{
				await Task.Delay(1000);

				List<CronJob> cronJobs = new();
				Dictionary<string, RunningJob> runningJobs = new();
				long loadedVersion = -1;
				DateTime lastLoad = DateTime.MinValue;

				while (true)
				{
					try
					{
						var now = SystemTime.OffsetUtcNow().DateTime;
						var version = Interlocked.Read(ref cronJobsVersion);
						// A drain (`drain app`) turns the scheduler off: no new run starts, runs that are going finish.
						if (!RuntimeMode.SchedulerEnabled)
						{
							await Task.Delay(1000);
							continue;
						}
						bool hasDueJob = GetDueJobs(cronJobs, now).Count > 0;

						if (hasDueJob || version != loadedVersion || now - lastLoad >= CronJobsReloadInterval)
						{
							loadedVersion = version;
							lastLoad = now;
							cronJobs = LoadCronJobs(settings, fileSystem, logger, now);
							cronJobs = await RunScheduledTasks(cronJobs, runningJobs, settings, engine, logger, pseudoRuntime, fileSystem);
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
						nextRun = GetNextRun(cronJob, now);
					}
					catch (Exception ex) when (ex is CrontabException || ex is ArgumentNullException)
					{
						logger.LogError($"Cron job '{cronJob.CronCommand}' calling {cronJob.GoalName} in {cronJob.AbsolutePrFilePath} is not a valid cron expression: {ex.Message}");
						continue;
					}
					cronJob = cronJob with { NextRun = nextRun };
					SaveCronJob(settings, cronJob);
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
		/// A job's run that has been started: Task completes when the goal has finished.
		/// </summary>
		private record RunningJob(Task Task, DateTime StartedUtc);

		/// <summary>
		/// Starts each due job in cronJobs as a "dont wait" goal call and stores its next run time. A job whose previous run is
		/// still going skips this occurrence. Returns cronJobs with the new NextRun values.
		/// </summary>
		private async Task<List<CronJob>> RunScheduledTasks(List<CronJob> cronJobs, Dictionary<string, RunningJob> runningJobs, ISettings settings, IEngine engine, ILogger logger, IPseudoRuntime pseudoRuntime, IPLangFileSystem fileSystem)
		{
			foreach (var finished in runningJobs.Where(p => p.Value.Task.IsCompleted).Select(p => p.Key).ToList())
			{
				runningJobs.Remove(finished);
			}

			var now = SystemTime.OffsetUtcNow().DateTime;
			var dueJobs = PlanDueJobs(cronJobs, now, job => runningJobs.ContainsKey(job.AbsolutePrFilePath));
			foreach (var (item, skip, nextRun) in dueJobs)
			{
				try
				{
					if (skip)
					{
						var running = runningJobs[item.AbsolutePrFilePath];
						logger.LogWarning($"Skipping cron run of {item.GoalName} ({item.Schedule}), its run started at {running.StartedUtc:HH:mm:ss} UTC is still going ({(now - running.StartedUtc).TotalSeconds:0} s). Next run {nextRun:HH:mm:ss} UTC");
						continue;
					}

					logger.LogInformation($"Running cron - GoalName:{item.GoalName}");

					// The goal runs on a rented engine with a copy of this memory stack, the same way as "call goal X, dont wait".
					// The copy starts empty so a job does not see variables of the step that started the scheduler or of other jobs.
					context.MemoryStack.Clear();
					var (_, task, error) = await pseudoRuntime.RunGoal(engine, contextAccessor, fileSystem.RelativeAppPath, item.GoalName, goal, waitForExecution: false, keepAliveKind: KeepAliveRegistry.Schedule);
					if (task is Task runTask)
					{
						runningJobs[item.AbsolutePrFilePath] = new RunningJob(runTask, now);
					}

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
				catch (Exception ex)
				{
					logger.LogError(ex, ex.ToString());
				}
				finally
				{
					SaveCronJob(settings, item with { NextRun = nextRun });

					var idx = cronJobs.IndexOf(item);
					if (idx != -1) cronJobs[idx] = item with { NextRun = nextRun };
				}
			}
			return cronJobs;
		}
	}
}
