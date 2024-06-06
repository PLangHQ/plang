using LightInject;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Secp256k1;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Errors.Handlers;
using PLang.Errors.Runtime;
using PLang.Events;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Modules;
using PLang.SafeFileSystem;
using PLang.Services.AppsRepository;
using PLang.Services.LlmService;
using PLang.Services.OutputStream;
using PLang.Utils;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection.Metadata.Ecma335;
using System.Text;

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
		Task<IError> RunGoal(Goal goal);
		Goal? GetGoal(string goalName, Goal? callingGoal = null);
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
		private IAppCache appCache;
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
			this.eventRuntime.SetContainer(container);
			this.eventRuntime.Load();

			this.typeHelper = container.GetInstance<ITypeHelper>();
			this.askUserHandlerFactory = container.GetInstance<IAskUserHandlerFactory>();

			this.OutputStreamFactory = container.GetInstance<IOutputStreamFactory>();
			this.prParser = container.GetInstance<PrParser>();
			this.memoryStack = container.GetInstance<MemoryStack>();
			this.appsRepository = container.GetInstance<IPLangAppsRepository>();
			this.appCache = container.GetInstance<IAppCache>();
			context.AddOrReplace(ReservedKeywords.MyIdentity, identityService.GetCurrentIdentity());

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

				var error = await eventRuntime.Load(false);
				if (error != null)
				{
					await HandleError(error);
					return;
				}

				var eventError = await eventRuntime.RunStartEndEvents(context, EventType.Before, EventScope.StartOfApp);
				if (eventError != null)
				{
					await HandleError(eventError);
					return;
				}

				error = await RunSetup();
				if (error != null)
				{
					await HandleError(error);
					return;
				}
				if (goalsToRun.Count == 1 && goalsToRun[0].ToLower().RemoveExtension() == "setup") return;

				StartScheduler();

				error = await RunStart(goalsToRun);
				if (error != null)
				{
					await HandleError(error);
					return;
				}

				error = await eventRuntime.RunStartEndEvents(context, EventType.After, EventScope.StartOfApp);
				if (error != null)
				{
					await HandleError(error);
					return;
				}

			}
			catch (Exception ex)
			{
				var error = new Error(ex.Message, Exception: ex);
				await HandleError(error);

			}
			finally
			{
				var alives = AppContext.GetData("KeepAlive") as List<Alive>;
				if (alives != null && alives.Count > 0)
				{
					logger.LogInformation("Keeping app alive, reasons:");
					foreach (var alive in alives)
					{
						logger.LogInformation(" - " + alive.Key);
					}

					while (alives != null && alives.Count > 0)
					{
						await Task.Delay(1000);
						alives = AppContext.GetData("KeepAlive") as List<Alive>;
					}
				}

				var error = await eventRuntime.RunStartEndEvents(context, EventType.Before, EventScope.EndOfApp);
				if (error != null)
				{
					await HandleError(error);
				}
			}
		}

		private async Task HandleError(IError error)
		{
			if (error is ErrorHandled) return;

			var appErrorEvent = await eventRuntime.AppErrorEvents(context, error);
			if (appErrorEvent != null)
			{
				var me = new MultipleError("Critical");
				me.Add(error);
				me.Add(appErrorEvent);

				await container.GetInstance<IErrorHandlerFactory>().CreateHandler().ShowError(me);
			}
		}

		private async Task<(bool, IError?)> HandleFileAccessError(FileAccessRequestError fare)
		{
			var fileAccessHandler = container.GetInstance<FileAccessHandler>();
			var askUserFileAccess = new AskUserFileAccess(fare.AppName, fare.Path, fare.Message, fileAccessHandler.ValidatePathResponse);
			
			return await askUserHandlerFactory.CreateHandler().Handle(askUserFileAccess);
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
				logger.LogDebug("Initiate new engine for scheduler");
				containerForScheduler = new ServiceContainer();
				((ServiceContainer)containerForScheduler).RegisterForPLangConsole(fileSystem.GoalsPath, Path.DirectorySeparatorChar.ToString());
			}

			var schedulerEngine = containerForScheduler.GetInstance<IEngine>();
			schedulerEngine.Init(containerForScheduler);
			Modules.ScheduleModule.Program.Start(containerForScheduler.GetInstance<ISettings>(),
				schedulerEngine, containerForScheduler.GetInstance<PrParser>(),
				containerForScheduler.GetInstance<ILogger>(), containerForScheduler.GetInstance<IPseudoRuntime>(),
				containerForScheduler.GetInstance<IPLangFileSystem>());
		}

		private async Task<IError?> RunSetup()
		{

			string setupFolder = Path.Combine(fileSystem.BuildPath, "Setup");
			if (!fileSystem.Directory.Exists(setupFolder)) return null;

			var files = fileSystem.Directory.GetFiles(setupFolder, ISettings.GoalFileName).ToList();
			if (files.Count == 0) return null;

			logger.LogDebug("Setup");
			foreach (var file in files)
			{
				var error = await RunGoal(file);
				if (error != null) return error;
			}
			return null;
		}

		private async Task<IError?> RunStart(List<string> goalNames)
		{
			var goalsToRun = GetStartGoals(goalNames);
			if (goalsToRun.Count == 0)
			{
				if (goalNames.Count == 0)
				{
					return new Error($"Could not find Start.goal to run. Are you in correct directory? I am running from {fileSystem.GoalsPath}. If you want to run specific goal file, for example Test.goal, you must run it like this: 'plang run Test'");
				}
				else
				{
					return new Error($"Goal file(s) not found to run. Are you in correct directory? I am running from {fileSystem.GoalsPath}");
				}
			}
			logger.LogDebug("Start");
			foreach (var folder in goalsToRun)
			{
				var error = await RunGoal(folder);
				if (error != null) return error;
			}
			return null;
		}

		public async Task<IError?> RunGoal(string prFileAbsolutePath)
		{
			if (!fileSystem.File.Exists(prFileAbsolutePath))
			{
				return new Error($"{prFileAbsolutePath} could not be found. Not running goal");
			}

			var stopwatch = new Stopwatch();
			stopwatch.Start();

			var goal = prParser.GetGoal(prFileAbsolutePath);
			if (goal == null)
			{
				return new Error($"Could not load pr file at {prFileAbsolutePath}");
			}

			foreach (var injection in goal.Injections)
			{
				((ServiceContainer)container).RegisterForPLangUserInjections(injection.Type, injection.Path, injection.IsGlobal);
			}

			var error = await RunGoal(goal);

			stopwatch.Stop();
			logger.LogDebug("Total time:" + stopwatch.ElapsedMilliseconds);

			return error;
		}

		private void SetLogLevel(string? goalComment)
		{
			if (goalComment == null) return;

			string[] logLevels = ["trace", "debug", "information", "warning", "error"];
			foreach (var logLevel in logLevels)
			{
				if (goalComment.ToLower().Contains($"[{logLevel}]"))
				{
					AppContext.SetData("GoalLogLevelByUser", Microsoft.Extensions.Logging.LogLevel.Trace);
					return;
				}
			}
			return;
		}

		public async Task<IError?> RunGoal(Goal goal)
		{
			AppContext.SetSwitch("Runtime", true);
			SetLogLevel(goal.Comment);

			int goalStepIndex = -1;
			try
			{
				logger.LogTrace("RootDirectory:{0}", fileSystem.RootDirectory);
				foreach (var injection in goal.Injections)
				{
					container.RegisterForPLangUserInjections(injection.Type, injection.Path, injection.IsGlobal, injection.EnvironmentVariable, injection.EnvironmentVariableValue);
				}

				logger.LogDebug("Goal: " + goal.GoalName);

				var eventError = await eventRuntime.RunGoalEvents(context, EventType.Before, goal);
				if (eventError != null) return eventError;

				//if (await CachedGoal(goal)) return null;

				for (goalStepIndex = 0; goalStepIndex < goal.GoalSteps.Count; goalStepIndex++)
				{
					var runStep = await RunStep(goal, goalStepIndex);
					if (runStep != null)
					{
						runStep = await HandleGoalError(runStep, goal, goalStepIndex);
						if (runStep != null) return runStep;
					}
				}
				//await CacheGoal(goal);

				eventError = await eventRuntime.RunGoalEvents(context, EventType.After, goal);
				return eventError;

			}
			catch (Exception ex)
			{
				var error = new Error(ex.Message, Exception: ex);
				if (context.ContainsKey(ReservedKeywords.IsEvent)) return error;

				var eventError = await HandleGoalError(error, goal, goalStepIndex);
				return eventError;
			}
			finally
			{
				AppContext.SetData("GoalLogLevelByUser", null);

				var os = OutputStreamFactory.CreateHandler();
				if (os is UIOutputStream && goal.ParentGoal == null)
				{
					((UIOutputStream)os).Flush();
				}
			}

		}
		/*
		private async Task<bool> CachedGoal(Goal goal)
		{
			if (goal.GoalInfo?.CachingHandler?.CacheKey == null) return false;

			var bytes = await appCache.Get(goal.GoalInfo.CachingHandler.CacheKey) as byte[];
			if (bytes == null || bytes.Length == 0)
			{
				// set the output stream as MemoryOutputStream because we want to cache the output result
				OutputStreamFactory.SetContext(typeof(MemoryOutputStream).FullName);
				return false;
			}

			var content = Encoding.UTF8.GetString(bytes);

			var handler = OutputStreamFactory.CreateHandler();
			await handler.WriteToBuffer(content);

			return true;
		}

		private async Task CacheGoal(Goal goal)
		{
			if (goal.GoalInfo?.CachingHandler?.CacheKey == null) return;

			var handler = OutputStreamFactory.CreateHandler(typeof(MemoryOutputStream).FullName);
			if (handler.Stream is not MemoryStream) return;

			handler.Stream.Seek(0, SeekOrigin.Begin);
			var bytes = await ((MemoryStream)handler.Stream).ReadBytesAsync((int)handler.Stream.Length);

			if (goal.GoalInfo.CachingHandler.CachingType == 1)
			{
				TimeSpan slidingExpiration = TimeSpan.FromMilliseconds(goal.GoalInfo.CachingHandler.TimeInMilliseconds);
				await appCache.Set(goal.GoalInfo.CachingHandler.CacheKey, bytes, slidingExpiration);
			}
			else
			{
				DateTimeOffset absoluteExpiration = DateTimeOffset.UtcNow.AddMilliseconds(goal.GoalInfo.CachingHandler.TimeInMilliseconds);
				await appCache.Set(goal.GoalInfo.CachingHandler.CacheKey, bytes, absoluteExpiration);
			}

			//reset the output stream
			OutputStreamFactory.SetContext(null);


			var content = Encoding.UTF8.GetString(bytes);
			handler = OutputStreamFactory.CreateHandler();
			await handler.WriteToBuffer(content);
			return;

		}*/

		private async Task<IError?> HandleGoalError(IError error, Goal goal, int goalStepIndex)
		{
			if (error is IErrorHandled) return error;

			var eventError = await eventRuntime.RunGoalErrorEvents(context, goal, goalStepIndex, error);
			return eventError;
		}

		private async Task<IError?> RunStep(Goal goal, int goalStepIndex, int retryCount = 0)
		{
			var step = goal.GoalSteps[goalStepIndex];
			try
			{
				if (HasExecuted(step)) return null;

				var error = await eventRuntime.RunStepEvents(context, EventType.Before, goal, step);
				if (error != null) return error;

				logger.LogDebug($"- {step.Text.Replace("\n", "").Replace("\r", "").MaxLength(80)}");

				var prError = await ProcessPrFile(goal, step, goalStepIndex);
				if (prError != null)
				{
					var result = await HandleStepError(goal, step, goalStepIndex, prError, retryCount);
					if (result != null) return result;
				}

				error = await eventRuntime.RunStepEvents(context, EventType.After, goal, step);
				return error;
			}
			catch (Exception stepException)
			{
				if (step.RetryHandler != null && step.RetryHandler.RetryCount > retryCount)
				{
					await Task.Delay(step.RetryHandler.RetryDelayInMilliseconds);
					return await RunStep(goal, goalStepIndex, ++retryCount);
				}
				else
				{
					var error = new ExceptionError(stepException);
					var result = await HandleStepError(goal, step, goalStepIndex, error, retryCount);

					return result;

				}
			}

		}


		private async Task<IError?> HandleStepError(Goal goal, GoalStep step, int goalStepIndex, IError error, int retryCount)
		{
			if (error is IErrorHandled) return error;

			if (error is Errors.AskUser.AskUserError aue)
			{
				(var isHandled, var handlerError) = await askUserHandlerFactory.CreateHandler().Handle(aue);
				if (handlerError != null)
				{
					return ErrorHelper.GetMultipleError(error, handlerError);
				}

				return await RunStep(goal, goalStepIndex);

			}

			if (error is FileAccessRequestError fare)
			{
				(var isHandled, var handlerError) = await HandleFileAccessError(fare);
				if (handlerError != null)
				{
					return ErrorHelper.GetMultipleError(error, handlerError);
				}

				return await RunStep(goal, goalStepIndex);

			}

			//let retry the step if user defined so
			if (step.RetryHandler != null && step.RetryHandler.RetryCount > retryCount)
			{
				await Task.Delay(step.RetryHandler.RetryDelayInMilliseconds);
				return await RunStep(goal, goalStepIndex, ++retryCount);
			}

			var eventRuntime = container.GetInstance<IEventRuntime>();
			(var continueNextStep, var eventError) = await eventRuntime.RunOnErrorStepEvents(context, error, goal, goal.GoalSteps[goalStepIndex], step.ErrorHandler);

			return (continueNextStep) ? null : eventError;
		}

		private bool HasExecuted(GoalStep step)
		{
			if (!step.Execute) return true;
			if (!step.RunOnce) return false;
			if (step.Executed == DateTime.MinValue) return false;
			if (settings.IsDefaultSystemDbPath && step.Executed != null && step.Executed != DateTime.MinValue) return true;

			var setupOnceDictionary = settings.GetOrDefault<Dictionary<string, DateTime>>(typeof(Engine), "SetupRunOnce", new());

			if (setupOnceDictionary == null || !setupOnceDictionary.ContainsKey(step.RelativePrPath))
			{
				step.Executed = DateTime.MinValue;
				return false;
			}

			step.Executed = setupOnceDictionary[step.RelativePrPath];
			return true;
		}


		public async Task<IError?> ProcessPrFile(Goal goal, GoalStep goalStep, int stepIndex)
		{
			if (goalStep.RunOnce && HasExecuted(goalStep))
			{
				return null;
			}

			if (!fileSystem.File.Exists(goalStep.AbsolutePrFilePath))
			{
				return new Error($"Could not find pr file {goalStep.RelativePrPath}. Maybe try to build again?. This step is defined in Goal at {goal.RelativeGoalPath}. The location of it on drive should be {goalStep.AbsolutePrFilePath}.");
			}

			var instruction = prParser.ParseInstructionFile(goalStep);
			if (instruction == null)
			{
				return new Error($"Module could not be loaded for {goalStep.RelativePrPath}");
			}

			if (stepIndex < goal.GoalSteps.Count && !goal.GoalSteps[stepIndex].Execute)
			{
				logger.LogDebug($"Step is disabled: {goal.GoalSteps[stepIndex].Execute}");
				return null;
			}

			Type? classType = typeHelper.GetRuntimeType(goalStep.ModuleType);
			if (classType == null)
			{
				return new Error("Could not find module:" + goalStep.ModuleType);
			}

			context.AddOrReplace(ReservedKeywords.Goal, goal);
			context.AddOrReplace(ReservedKeywords.Step, goalStep);
			context.AddOrReplace(ReservedKeywords.Instruction, instruction);

			var classInstance = container.GetInstance(classType) as BaseProgram;
			if (classInstance == null)
			{
				return new Error($"Could not create instance of {classType}");
			}

			var llmServiceFactory = container.GetInstance<ILlmServiceFactory>();
			var appCache = container.GetInstance<IAppCache>();

			classInstance.Init(container, goal, goalStep, instruction, memoryStack, logger, context,
				typeHelper, llmServiceFactory, settings, appCache, this.HttpContext);

			using (var cts = new CancellationTokenSource())
			{
				long executionTimeout = (goalStep.CancellationHandler == null) ? 30 * 1000 : goalStep.CancellationHandler.CancelExecutionAfterXMilliseconds;
				cts.CancelAfter(TimeSpan.FromMilliseconds(executionTimeout));

				try
				{
					var task = classInstance.Run();
					await task;

					var errors = task.Result;
					if (errors != null)
					{
						return errors;
					}
					if (goalStep.RunOnce)
					{
						var dict = settings.GetOrDefault<Dictionary<string, DateTime>>(typeof(Engine), "SetupRunOnce", new());
						if (dict == null) dict = new();

						dict.TryAdd(goalStep.RelativePrPath, DateTime.UtcNow);
						settings.Set<Dictionary<string, DateTime>>(typeof(Engine), "SetupRunOnce", dict);
					}
					return null;
				}
				catch (OperationCanceledException)
				{
					if (goalStep.CancellationHandler?.GoalNameToCallAfterCancellation != null)
					{
						var engine = container.GetInstance<IEngine>();
						var pseudoRuntime = container.GetInstance<IPseudoRuntime>();
						var parameters = context.GetReserverdKeywords();
						await pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath, goalStep.CancellationHandler.GoalNameToCallAfterCancellation, parameters, goal);
						return null;
					}
					return new Error("Step was cancelled because it ran for to long. To extend the timeout, include timeout in your step.");
				}
				catch (Exception ex)
				{
					return new Error(ex.Message, Exception: ex);
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

		public Goal? GetGoal(string goalName, Goal? callingGoal)
		{
			goalName = goalName.AdjustPathToOs();
			var goal = prParser.GetGoalByAppAndGoalName(fileSystem.RootDirectory, goalName, callingGoal);
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
