using LightInject;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using PLang.Building.Events;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Modules;
using PLang.SafeFileSystem;
using PLang.Services.AppsRepository;
using PLang.Services.OutputStream;
using PLang.Utils;
using System.Diagnostics;
using System.Net;
using System.Reflection.Metadata.Ecma335;

namespace PLang.Runtime
{
    public interface IEngine
	{
		IOutputStreamFactory OutputStreamFactory { get; }
		void AddContext(string key, object value);
		PLangAppContext GetContext();
		MemoryStack GetMemoryStack();
		void Init(IServiceContainer container);
		Task Run(List<string> goalsToRun);
		Task RunGoal(Goal goal);
		Goal? GetGoal(string goalName);
		List<Goal> GetGoalsAvailable(string appPath, string goalName);

		public HttpListenerContext? HttpContext { get; set; }
	}
	public record Alive(Type Type, string Key);
	public class Engine : IEngine
	{
		private IServiceContainer container;

		private IPLangFileSystem fileSystem;
		private IPLangIdentityService identityService;
		private ILogger logger;
		private ISettings settings;
		private IEventRuntime eventRuntime;
		private ITypeHelper typeHelper;
		private IAskUserHandlerFactory askUserHandlerFactory;
		public IOutputStreamFactory OutputStreamFactory { get; private set; }
		private IPLangAppsRepository appsRepository;
		private PrParser prParser;
		private MemoryStack memoryStack;
		private PLangAppContext context;
		public HttpListenerContext? HttpContext { get; set; }

		public Engine()
		{
		}

		public void Init(IServiceContainer container)
		{
			this.container = container;

			this.context = container.GetInstance<PLangAppContext>();
			this.fileSystem = container.GetInstance<IPLangFileSystem>();
			this.identityService = container.GetInstance<IPLangIdentityService>();
			this.logger = container.GetInstance<ILogger>();
			this.settings = container.GetInstance<ISettings>();
			this.eventRuntime = container.GetInstance<IEventRuntime>();
			this.typeHelper = container.GetInstance<ITypeHelper>();
			this.askUserHandlerFactory = container.GetInstance<IAskUserHandlerFactory>();

			this.OutputStreamFactory = container.GetInstance<IOutputStreamFactory>();
			this.prParser = container.GetInstance<PrParser>();
			this.memoryStack = container.GetInstance<MemoryStack>();
			this.appsRepository = container.GetInstance<IPLangAppsRepository>();
			context.AddOrReplace(ReservedKeywords.MyIdentity, identityService.GetCurrentIdentity());

			prParser.LoadAllGoals();
		}

		public MemoryStack GetMemoryStack() => this.memoryStack;

		public void AddContext(string key, object value)
		{
			if (ReservedKeywords.IsReserved(key))
			{
				throw new ReservedKeywordException($"{key} is reserved for the system. Choose a different name");
			}

			this.context.AddOrReplace(key, value);
		}
		public PLangAppContext GetContext() => this.context;

		public async Task Run(List<string> goalsToRun)
		{
			AppContext.SetSwitch("Runtime", true);
			try
			{
				logger.LogInformation("App Start:" + DateTime.Now.ToLongTimeString());

				await eventRuntime.Load(container, false);
				await eventRuntime.RunStartEndEvents(context, EventType.Before, EventScope.StartOfApp);

				await RunSetup();
				if (goalsToRun.Count == 1 && goalsToRun[0].ToLower().RemoveExtension() == "setup") return;

				StartScheduler();

				await RunStart(goalsToRun);
				await eventRuntime.RunStartEndEvents(context, EventType.After, EventScope.StartOfApp);

			}
			catch (Exception ex)
			{
				context.AddOrReplace(ReservedKeywords.Exception, ex);
				await eventRuntime.RunStartEndEvents(context, EventType.OnError, EventScope.RunningApp);

				
				context.Remove(ReservedKeywords.Exception);
			}
			finally
			{
				var alives = AppContext.GetData("KeepAlive") as List<Alive>;
				if (alives != null && alives.Count > 0)
				{
					logger.LogInformation("Keeping app alive, reasons:");
					foreach (var alive in alives)
					{
						logger.LogInformation(alive.Key);
					}

					while (alives != null && alives.Count > 0)
					{
						await Task.Delay(1000);
						alives = AppContext.GetData("KeepAlive") as List<Alive>;
					}
				}


				await eventRuntime.RunStartEndEvents(context, EventType.Before, EventScope.EndOfApp);
			}
		}

		private async Task<bool> HandleFileAccessException(FileAccessException ex)
		{
			var fileAccessHandler = new FileAccessHandler(settings, container.GetInstance<ILlmService>(), logger, fileSystem);
			var askUserFileException = new AskUserFileAccess(ex.AppName, ex.Path, ex.Message, fileAccessHandler.ValidatePathResponse);

			return await askUserHandlerFactory.CreateHandler().Handle(askUserFileException);
		}

		private void StartScheduler()
		{
			IServiceContainer containerForScheduler;
			if (this.OutputStreamFactory.CreateHandler() is UIOutputStream)
			{
				containerForScheduler = container;
			}
			else
			{
				containerForScheduler = new ServiceContainer();
				((ServiceContainer) containerForScheduler).RegisterForPLangConsole(fileSystem.GoalsPath, Path.DirectorySeparatorChar.ToString());
			}
			

			var schedulerEngine = containerForScheduler.GetInstance<IEngine>();
			schedulerEngine.Init(containerForScheduler);
			Modules.ScheduleModule.Program.Start(containerForScheduler.GetInstance<ISettings>(),
				schedulerEngine, containerForScheduler.GetInstance<PrParser>(),
				containerForScheduler.GetInstance<ILogger>(), containerForScheduler.GetInstance<IPseudoRuntime>(),
				containerForScheduler.GetInstance<IPLangFileSystem>());
		}

		private async Task RunSetup()
		{

			string setupFolder = Path.Combine(fileSystem.BuildPath, "Setup");
			if (!fileSystem.Directory.Exists(setupFolder)) return;

			var files = fileSystem.Directory.GetFiles(setupFolder, ISettings.GoalFileName).ToList();
			if (files.Count == 0) return;

			logger.LogDebug("Setup");
			foreach (var file in files)
			{
				await RunGoal(file);
			}
		}

		private async Task RunStart(List<string> goalNames)
		{
			var goalsToRun = GetStartGoals(goalNames);
			if (goalsToRun.Count == 0)
			{
				if (goalNames.Count == 0)
				{
					logger.LogWarning($"Could not find Start.goal to run. Are you in correct directory? I am running from {fileSystem.GoalsPath}. If you want to run specific goal file, for example Test.goal, you must run it like this: 'plang run Test'");
				}
				else
				{
					logger.LogWarning($"Goal file(s) not found to run. Are you in correct directory? I am running from {fileSystem.GoalsPath}");
				}

				return;
			}
			logger.LogDebug("Start");
			foreach (var folder in goalsToRun)
			{
				await RunGoal(folder);
			}
		}

		public async Task RunGoal(string prFileAbsolutePath)
		{
			if (!fileSystem.File.Exists(prFileAbsolutePath))
			{
				logger.LogWarning($"{prFileAbsolutePath} could not be found. Not running goal");
				return;
			}

			var stopwatch = new Stopwatch();
			stopwatch.Start();

			var goal = prParser.GetGoal(prFileAbsolutePath);
			if (goal == null)
			{
				logger.LogWarning($"Could not load pr file at {prFileAbsolutePath}");
				return;
			}

			foreach (var injection in goal.Injections)
			{
				((ServiceContainer)container).RegisterForPLangUserInjections(injection.Type, injection.Path, injection.IsGlobal);
			}

			await RunGoal(goal);

			stopwatch.Stop();
			logger.LogDebug("Total time:" + stopwatch.ElapsedMilliseconds);
		}

		private bool IsLogLevel(string goalComment)
		{
			string[] logLevels = ["trace", "debug", "information", "warning", "error"];
			foreach (var logLevel in logLevels)
			{
				if (goalComment.ToLower().Contains($"[{logLevel}]")) return true;
			}
			return false;
		}

		public async Task RunGoal(Goal goal)
		{
			if (goal.Comment != null && IsLogLevel(goal.Comment))
			{
				AppContext.SetData("GoalLogLevelByUser", Microsoft.Extensions.Logging.LogLevel.Trace);
			}

			int goalStepIndex = 0;
			try
			{
				logger.LogTrace("RootDirectory:{0}", fileSystem.RootDirectory);
				foreach (var injection in goal.Injections)
				{
					container.RegisterForPLangUserInjections(injection.Type, injection.Path, injection.IsGlobal);
				}

				logger.LogDebug("Goal: " + goal.GoalName);
				await eventRuntime.RunGoalEvents(context, EventType.Before, goal);

				for (; goalStepIndex < goal.GoalSteps.Count; goalStepIndex++)
				{
					await RunStep(goal, goalStepIndex);
				}

				await eventRuntime.RunGoalEvents(context, EventType.After, goal);

			}
			catch (RuntimeProgramException)
			{
				throw;
			}
			catch (RuntimeUserStepException rse)
			{

				if (rse.StatusCode >= 500)
				{
					await eventRuntime.RunGoalErrorEvents(context, goal, goalStepIndex, rse);
					throw;
				}
				else
				{
					await OutputStreamFactory.CreateHandler().Write(rse.Message, rse.Type, rse.StatusCode);
				}
			}
			catch (RuntimeGoalEndException ex)
			{
				//this equals to doing return in a function
				logger.LogDebug(ex.Message, ex);
			}
			catch (Exception ex)
			{
				if (context.ContainsKey(ReservedKeywords.IsEvent)) throw;

				await eventRuntime.RunGoalErrorEvents(context, goal, goalStepIndex, ex);
				throw;
			}
			finally
			{
				if (goal.Comment != null && IsLogLevel(goal.Comment))
				{
					AppContext.SetData("GoalLogLevelByUser", null);
				}
				var os = OutputStreamFactory.CreateHandler();
				if (os is UIOutputStream && goal.ParentGoal == null)
				{
					((UIOutputStream) os).Flush();
				}
			}

		}

		private async Task RunStep(Goal goal, int goalStepIndex, int retryCount = 0)
		{
			var step = goal.GoalSteps[goalStepIndex];
			try
			{
				if (step.Execute && step.Executed == null)
				{
					await eventRuntime.RunStepEvents(context, EventType.Before, goal, step);
				}

				logger.LogDebug($"- {step.Text.Replace("\n", "").Replace("\r", "").MaxLength(80)}");

				await ProcessPrFile(goal, step, goalStepIndex);
				if (step.Execute && step.Executed == null)
				{
					await eventRuntime.RunStepEvents(context, EventType.After, goal, step);
				}

			}
			catch (FileAccessException fae)
			{
				if (await HandleFileAccessException(fae))
				{
					await RunStep(goal, goalStepIndex);
				}
				else
				{
					throw;
				}

			}
			catch (AskUserException aue)
			{
				var result = await askUserHandlerFactory.CreateHandler().Handle(aue);
				if (result)
				{
					await RunStep(goal, goalStepIndex);
				}
				else
				{
					throw;
				}
			}
			catch (Exception stepException)
			{
				if (step.RetryHandler != null && step.RetryHandler.RetryCount > retryCount)
				{
					await Task.Delay(step.RetryHandler.RetryDelayInMilliseconds);
					await RunStep(goal, goalStepIndex, ++retryCount);
				}
				else
				{
					var eventRuntime = container.GetInstance<IEventRuntime>();
					var continueNextStep = await eventRuntime.RunOnErrorStepEvents(context, stepException, goal, goal.GoalSteps[goalStepIndex], step.ErrorHandler);
					if (continueNextStep) return;

					throw;					
				}
			}
		}



		public async Task ProcessPrFile(Goal goal, GoalStep goalStep, int stepIndex)
		{
			if (goalStep.RunOnce && goalStep.Executed != null)
			{
				return;
			}

			if (!fileSystem.File.Exists(goalStep.AbsolutePrFilePath))
			{
				throw new FileNotFoundException($"Could not find pr file {goalStep.RelativePrPath}. Maybe try to build again?. This step is defined in Goal at {goal.RelativeGoalPath}. The location of it on drive should be {goalStep.AbsolutePrFilePath}.");
			}

			var instruction = prParser.ParseInstructionFile(goalStep);
			if (instruction == null)
			{
				logger.LogWarning($"Module could not be loaded for {goalStep.RelativePrPath}");
				return;
			}

			if (stepIndex < goal.GoalSteps.Count && !goal.GoalSteps[stepIndex].Execute)
			{
				logger.LogDebug($"Step is disabled: {goal.GoalSteps[stepIndex].Execute}");
				return;
			}

			Type? classType = typeHelper.GetRuntimeType(goalStep.ModuleType);
			if (classType == null)
			{
				logger.LogWarning("Could not find module:" + goalStep.ModuleType);
				return;
			}

			context.AddOrReplace(ReservedKeywords.Goal, goal);
			context.AddOrReplace(ReservedKeywords.Step, goalStep);
			context.AddOrReplace(ReservedKeywords.Instruction, instruction);

			var classInstance = container.GetInstance(classType) as BaseProgram;
			if (classInstance == null) return;

			var llmService = container.GetInstance<ILlmService>();
			var appCache = container.GetInstance<IAppCache>(context[ReservedKeywords.Inject_Caching]!.ToString());

			classInstance.Init(container, goal, goalStep, instruction, memoryStack, logger, context,
				typeHelper, llmService, settings, appCache, this.HttpContext);

			using (var cts = new CancellationTokenSource())
			{
				long executionTimeout = (goalStep.CancellationHandler == null) ? 30 * 1000 : goalStep.CancellationHandler.CancelExecutionAfterXMilliseconds;
				cts.CancelAfter(TimeSpan.FromMilliseconds(executionTimeout));

				try
				{
					var task = classInstance.Run();

					await task;
					if (goalStep.RunOnce)
					{
						var dict = settings.GetOrDefault<Dictionary<string, DateTime>>(typeof(Engine), "SetupRunOnce", new());
						if (dict == null) dict = new();

						dict.TryAdd(goalStep.RelativePrPath, DateTime.UtcNow);
						settings.Set<Dictionary<string, DateTime>>(typeof(Engine), "SetupRunOnce", dict);
					}
				}
				catch (OperationCanceledException)
				{
					if (goalStep.CancellationHandler?.GoalNameToCallAfterCancellation != null)
					{
						var engine = container.GetInstance<IEngine>();
						var pseudoRuntime = container.GetInstance<IPseudoRuntime>();
						var parameters = context.GetReserverdKeywords();
						await pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath, goalStep.CancellationHandler.GoalNameToCallAfterCancellation, parameters, goal);
					}
					throw;
				}
				catch (Exception)
				{
					throw;
				}
				finally
				{
					// reset the Execute to false on all steps inside if statement
					if (goalStep.Indent > 0)
					{
						goalStep.Execute = false;
					}
				}
			}
		}




		private List<string> GetStartGoals(List<string> goalNames)
		{
			List<string> goalsToRun = new();
			if (goalNames.Count > 0)
			{
				var goalFiles = fileSystem.Directory.GetFiles(fileSystem.BuildPath, ISettings.GoalFileName, SearchOption.AllDirectories).ToList();
				foreach (var goalName in goalNames)
				{
					if (string.IsNullOrEmpty(goalName)) continue;

					string name = goalName.AdjustPathToOs().Replace(".goal", "").ToLower();
					if (name.StartsWith(".")) name = name.Substring(1);

					var folderPath = goalFiles.FirstOrDefault(p => p.ToLower() == Path.Join(fileSystem.RootDirectory, ".build", name, ISettings.GoalFileName).ToLower());
					if (folderPath != null)
					{
						goalsToRun.Add(folderPath);
					}
					else
					{
						logger.LogError($"{goalName} could not be found. It will not run. Startup path: {fileSystem.RootDirectory}");
						return new();
					}
				}

				return goalsToRun;
			}

			if (!fileSystem.Directory.Exists(Path.Join(fileSystem.BuildPath, "Start")))
			{
				return new();
			}

			var startFile = fileSystem.Directory.GetFiles(Path.Join(fileSystem.BuildPath, "Start"), ISettings.GoalFileName, SearchOption.AllDirectories).FirstOrDefault();
			if (startFile != null)
			{
				goalsToRun.Add(startFile);
				return goalsToRun;
			}

			var files = fileSystem.Directory.GetFiles(fileSystem.GoalsPath, "*.goal");
			if (files.Length > 1)
			{
				logger.LogError("Could not decide on what goal to run. More then 1 goal file is in directory. You send the goal name as parameter when you run plang");
				return new();
			}
			else if (files.Length == 1)
			{
				goalsToRun.Add(files[0]);
			}
			else
			{
				logger.LogError($"No goal file could be found in directory. Are you in the correct directory? I am running from {fileSystem.GoalsPath}");
				return new();
			}

			return goalsToRun;

		}

		public Goal? GetGoal(string goalName)
		{
			goalName = goalName.AdjustPathToOs();
			var goal = prParser.GetGoalByAppAndGoalName(fileSystem.RootDirectory, goalName);
			if (goal == null && goalName.TrimStart(Path.DirectorySeparatorChar).StartsWith("apps"))
			{
				appsRepository.InstallApp(goalName);
				goal = prParser.GetGoalByAppAndGoalName(fileSystem.RootDirectory, goalName);
			}
			return goal;
		}



		public List<Goal> GetGoalsAvailable(string appPath, string goalName)
		{
			return prParser.GetAllGoals();
		}
	}
}
