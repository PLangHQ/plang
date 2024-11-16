using System.ComponentModel;
using LightInject;
using Microsoft.Extensions.Logging;
using NCrontab;
using Newtonsoft.Json;
using PLang.Attributes;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Utils;

namespace PLang.Modules.ScheduleModule;

[Description("Wait, Sleep and time delay. Cron scheduler")]
public class Program : BaseProgram
{
    private readonly IEngine engine;
    private readonly IPLangFileSystem fileSystem;
    private readonly ILogger logger;
    private readonly ModuleSettings moduleSettings;
    private readonly IOutputStreamFactory outputStreamFactory;
    private readonly IPseudoRuntime pseudoRuntime;
    private readonly ISettings settings;

    public Program(ISettings settings, PrParser prParser, IEngine engine, IPseudoRuntime pseudoRuntime, ILogger logger,
        IPLangFileSystem fileSystem, IOutputStreamFactory outputStreamFactory)
    {
        this.settings = settings;
        PrParser = prParser;
        this.engine = engine;
        this.pseudoRuntime = pseudoRuntime;
        this.logger = logger;
        this.fileSystem = fileSystem;
        this.outputStreamFactory = outputStreamFactory;
        moduleSettings = new ModuleSettings(settings);
    }

    public PrParser PrParser { get; }

    [Description("WaitForExecution is always true when calling Sleep")]
    [MethodSettings(CanBeAsync = false)]
    public async Task Sleep(int sleepTimeInMilliseconds)
    {
        //make sure we always wait for execution
        goalStep.WaitForExecution = true;
        await Task.Delay(sleepTimeInMilliseconds);
    }

    [Description(
        "Use numerical representation for cronCommand, e.g. 0 11 * * 1. goalName is the goal that should be called, it should be prefixed by ! and be whole word with possible slash(/).")]
    public async Task Schedule(string cronCommand, GoalToCall goalName, Dictionary<string, object?>? parameters = null,
        DateTime? nextRun = null)
    {
        await Schedule(goalStep.AbsolutePrFilePath, cronCommand, goalName, parameters, nextRun);
    }

    private async Task Schedule(string absolutePrFilePath, string cronCommand, GoalToCall goalName,
        Dictionary<string, object?>? parameters = null, DateTime? nextRun = null)
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

    public async Task StartScheduler()
    {
        var engine = this.engine;
        var settings = this.settings;
        var prParser = PrParser;
        var logger = this.logger;
        var pseudoRuntime = this.pseudoRuntime;
        var fileSystem = this.fileSystem;

        if (outputStreamFactory.CreateHandler() is not UIOutputStream)
        {
            logger.LogDebug("Initiate new engine for scheduler");
            var containerForScheduler = new ServiceContainer();
            ((ServiceContainer)containerForScheduler).RegisterForPLangConsole(fileSystem.GoalsPath,
                fileSystem.Path.DirectorySeparatorChar.ToString());

            engine = containerForScheduler.GetInstance<IEngine>();
            engine.Init(containerForScheduler);
            settings = containerForScheduler.GetInstance<ISettings>();
            prParser = containerForScheduler.GetInstance<PrParser>();
            logger = containerForScheduler.GetInstance<ILogger>();
            pseudoRuntime = containerForScheduler.GetInstance<IPseudoRuntime>();
            fileSystem = containerForScheduler.GetInstance<IPLangFileSystem>();
        }


        Start(settings, engine, prParser, logger, pseudoRuntime, fileSystem);
    }


    private void Start(ISettings settings, IEngine engine, PrParser prParser, ILogger logger,
        IPseudoRuntime pseudoRuntime, IPLangFileSystem fileSystem)
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

    private void RunContainer(ISettings settings, IEngine engine, PrParser prParser, ILogger logger,
        IPseudoRuntime pseudoRuntime, IPLangFileSystem fileSystem)
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
                await RunScheduledTasks(settings, engine, prParser, logger, pseudoRuntime, fileSystem,
                    outputStreamFactory);

                //run every 1 min
                await Task.Delay(60 * 1000);
            }
        }));
    }

    private List<CronJob> CleanDeletedCronJobs(ISettings settings, IPLangFileSystem fileSystem, List<CronJob> cronJobs)
    {
        for (var i = 0; i < cronJobs.Count; i++)
        {
            var cronJob = cronJobs[i];
            if (cronJob.AbsolutePrFilePath == null || !fileSystem.File.Exists(cronJob.AbsolutePrFilePath))
            {
                cronJobs.RemoveAt(i);
                i--;
            }
        }

        settings.SetList(typeof(ModuleSettings), cronJobs);
        return cronJobs;
    }


    private async Task RunScheduledTasks(ISettings settings, IEngine engine, PrParser prParser, ILogger logger,
        IPseudoRuntime pseudoRuntime, IPLangFileSystem fileSystem, IOutputStreamFactory outputStreamFactory)
    {
        logger.LogDebug("Running 1 min cron check");
        CronJob? item = null;
        try
        {
            var list = settings.GetValues<CronJob>(typeof(ModuleSettings)).Where(p => !p.IsArchived).ToList();

            for (var i = 0; i < list.Count; i++)
            {
                item = list[i];

                var p = new Program(settings, prParser, engine, pseudoRuntime, logger, fileSystem, outputStreamFactory);
                var schedule = CrontabSchedule.Parse(item.CronCommand);

                var nextOccurrence = schedule.GetNextOccurrence(SystemTime.OffsetUtcNow().DateTime);
                if (item.NextRun == null)
                {
                    await p.Schedule(item.AbsolutePrFilePath, item.CronCommand, item.GoalName, item.Parameters,
                        nextOccurrence);
                    continue;
                }

                if (item.NextRun > SystemTime.OffsetUtcNow().DateTime) continue;

                logger.LogDebug($"Running cron - GoalName:{item.GoalName}");

                using (var cts = new CancellationTokenSource())
                {
                    engine.GetMemoryStack().Clear();

                    var maxExecutionTime = item.MaxExecutionTimeInMilliseconds == 0
                        ? 30000
                        : item.MaxExecutionTimeInMilliseconds;
                    cts.CancelAfter(maxExecutionTime);
                    var result = await pseudoRuntime.RunGoal(engine, engine.GetContext(), fileSystem.RelativeAppPath,
                        item.GoalName, item.Parameters);
                    if (result.error != null) logger.LogError(result.error.ToString());
                }

                nextOccurrence = schedule.GetNextOccurrence(SystemTime.OffsetUtcNow().DateTime);
                await p.Schedule(item.AbsolutePrFilePath, item.CronCommand, item.GoalName, item.Parameters,
                    nextOccurrence);
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"{fileSystem.RelativeAppPath} doing cronjob {item!.CronCommand} calling {item!.GoalName} took longer then {item!.MaxExecutionTimeInMilliseconds}ms to run.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
        }
    }

    public record CronJob(
        string AbsolutePrFilePath,
        string CronCommand,
        GoalToCall GoalName,
        Dictionary<string, object?>? Parameters = null,
        DateTime? NextRun = null,
        int MaxExecutionTimeInMilliseconds = 30000)
    {
        public bool IsArchived = false;
    }
}