using LightInject;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Org.BouncyCastle.Utilities.Zlib;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Errors.Events;
using PLang.Errors.Handlers;
using PLang.Errors.Interfaces;
using PLang.Errors.Runtime;
using PLang.Events;
using PLang.Events.Types;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules;
using PLang.SafeFileSystem;
using PLang.Services.AppsRepository;
using PLang.Services.LlmService;
using PLang.Services.OutputStream;
using PLang.Utils;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using static PLang.Modules.MockModule.Program;
using static PLang.Runtime.PseudoRuntime;
using static PLang.Utils.StepHelper;

namespace PLang.Runtime
{
	public interface IEngine : IDisposable
	{
		string Id { get; init; }
		public string Name { get; set; }
		IOutputStreamFactory OutputStreamFactory { get; }
		HttpContext? HttpContext { get; set; }
		void SetParentEngine(IEngine engine);
		IEngine? ParentEngine { get; }
		string Path { get; }

		public static readonly string DefaultEnvironment = "production";
		public string Environment { get; set; }
		GoalStep? CallingStep { get; }
		IPLangFileSystem FileSystem { get; }
		List<CallbackInfo>? CallbackInfos { get; set; }
		PrParser PrParser { get; }
		MemoryStack MemoryStack { get; }
		ConcurrentDictionary<string, Engine.LiveConnection> LiveConnections { get; set; }
		IOutputStream OutputStream { get; }
		List<MockData> Mocks { get; init; }

		void AddContext(string key, object value);
		PLangAppContext GetContext();
		MemoryStack GetMemoryStack();
		void Init(IServiceContainer container, PLangAppContext? context = null);
		Task<(object? Variables, IError? Error)> Run(string goalToRun);
		Task<(object? Variables, IError? Error)> RunGoal(Goal goal, uint waitForXMillisecondsBeforeRunningGoal = 0);
		Goal? GetGoal(string goalName, Goal? callingGoal = null);
		List<Goal> GetGoalsAvailable(string appPath, string goalName);
		Task<(object? ReturnValue, IError? Error)> RunFromStep(string prFile);
		Task<(object? ReturnValue, IError? Error)> ProcessPrFile(Goal goal, GoalStep goalStep, int stepIndex);
		IEventRuntime GetEventRuntime();

		EnginePool GetEnginePool(string rootPath);
		void SetCallingStep(GoalStep callingStep);
		void ReplaceContext(PLangAppContext pLangAppContext);
		void ReplaceMemoryStack(MemoryStack memoryStack);
		void Return(bool reset = false);
		void SetOutputStream(IOutputStream outputStream);
		Task<(object? Variables, IError? Error)> RunGoal(GoalToCallInfo goalToCall, Goal parentGoal, uint waitForXMillisecondsBeforeRunningGoal = 0);
		Task<IEngine> RentAsync(GoalStep callingStep, IOutputStream output);
		void Return(IEngine engine, bool reset = false);
	}
	public record Alive(Type Type, string Key, List<object> Instances) : IDisposable
	{
		public void Dispose()
		{
			foreach (var item in Instances)
			{
				if (item is IDisposable disposable) disposable.Dispose();
				if (item is IList list)
				{
					foreach (var item2 in list)
					{
						if (item2 is IDisposable disposable1) disposable1.Dispose();
					}
				}
			}
		}
	}

	public class Engine : IEngine, IDisposable
	{
		public string Id { get; init; }
		public string Name { get; set; }
		public string Environment { get; set; } = IEngine.DefaultEnvironment;

		public string Path { get { return fileSystem.RootDirectory; } }
		private bool disposed;

		private IServiceContainer container;

		private IPLangFileSystem fileSystem;
		private IPLangIdentityService identityService;
		private ILogger logger;
		private ISettings settings;
		private IEventRuntime eventRuntime;
		private ITypeHelper typeHelper;
		public IOutputStreamFactory OutputStreamFactory { get; private set; }
		private IOutputStream outputStream;

		private PrParser prParser;
		private MemoryStack memoryStack;
		private PLangAppContext context;
		public HttpContext? HttpContext { get; set; }
		public MemoryStack MemoryStack { get { return memoryStack; } }
		public PrParser PrParser { get { return prParser; } }

		public ConcurrentDictionary<string, LiveConnection> LiveConnections { get; set; } = new();
		public record LiveConnection(Microsoft.AspNetCore.Http.HttpResponse Response, bool IsFlushed)
		{
			public bool IsFlushed { get; set; } = IsFlushed;
		};
		public IOutputStream OutputStream { get { return outputStream; } }

		IEngine? _parentEngine = null;
		GoalStep? callingStep = null;
		public IEngine? ParentEngine { get { return _parentEngine; } }

		public Engine()
		{
			Id = Guid.NewGuid().ToString();
			AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
			{
				Console.WriteLine($"Unhandled exception: {args.ExceptionObject}");
			};
			this.Mocks = new();

		}
		public List<MockData> Mocks { get; init; }

		public void Init(IServiceContainer container, PLangAppContext? context = null)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			this.container = container;
			this.context = context ?? container.GetInstance<PLangAppContext>();
			this.fileSystem = container.GetInstance<IPLangFileSystem>();
			this.identityService = container.GetInstance<IPLangIdentityService>();
			this.logger = container.GetInstance<ILogger>();
			this.settings = container.GetInstance<ISettings>();
			this.eventRuntime = container.GetInstance<IEventRuntime>();
			this.eventRuntime.SetContainer(container);
			this.eventRuntime.Load();
			logger.LogDebug($" ---------- Init on Engine  ---------- {stopwatch.ElapsedMilliseconds}");
			this.typeHelper = container.GetInstance<ITypeHelper>();

			this.OutputStreamFactory = container.GetInstance<IOutputStreamFactory>();
			this.prParser = container.GetInstance<PrParser>();
			this.memoryStack = container.GetInstance<MemoryStack>();
			
			var outputStreamFactory = container.GetInstance<IOutputStreamFactory>();
			var outputStream = outputStreamFactory.CreateHandler();
			var memoryStack = container.GetInstance<MemoryStack>();

			var fileSystem = container.GetInstance<IPLangFileSystem>();
			var plangGlobal = new Dictionary<string, object>()
			{
				{ "output", outputStream.Output },
				{ "osPath", fileSystem.SystemDirectory },
				{ "rootPath", fileSystem.RootDirectory },
				{ "EngineUniqueId", Id}
			};
			this.context.AddOrReplace("!plang", plangGlobal);

			this.context.AddOrReplace(ReservedKeywords.MyIdentity, identityService.GetCurrentIdentity());
			logger.LogDebug($" ---------- Done Init on Engine  ---------- {stopwatch.ElapsedMilliseconds}");
		}




		public void SetParentEngine(IEngine parentEngine)
		{
			this._parentEngine = parentEngine;
			if (fileSystem.RootDirectory != parentEngine.Path)
			{
				fileSystem.AddFileAccess(new FileAccessControl(fileSystem.RootDirectory, parentEngine.Path, ProcessId: this.fileSystem.Id));
			}

			var activeEvents = parentEngine.GetEventRuntime().GetActiveEvents();
			this.eventRuntime.SetActiveEvents(activeEvents);
		}

		public List<CallbackInfo>? CallbackInfos { get; set; }

		public void SetOutputStream(IOutputStream outputStream)
		{
			this.outputStream = outputStream;
		}
		public IPLangFileSystem FileSystem { get { return fileSystem; } }
		public void ReplaceContext(PLangAppContext context)
		{
			this.context = context;
		}
		public void ReplaceMemoryStack(MemoryStack memoryStack)
		{
			this.memoryStack = memoryStack;
		}

		public async Task<IEngine> RentAsync(GoalStep callingStep, IOutputStream outputStream)
		{
			var enginePool = GetEnginePool(Path);
			return await enginePool.RentAsync(this, callingStep, Path, outputStream);
		}
		public void Return(IEngine engine, bool reset = false)
		{
			var enginePool = GetEnginePool(Path);
			enginePool.Return(engine, reset);
		}

		public void Return(bool reset = false)
		{
			if (ParentEngine == null)
			{
				throw new Exception($"Parent engine is null on return. {ErrorReporting.CreateIssueShouldNotHappen}");
			}

			context = ParentEngine.GetContext();
			memoryStack.Clear();
			callingStep = null;

			if (outputStream is HttpOutputStream hos)
			{
				hos.MainResponseIsDone = true;
			}

			outputStream = ParentEngine.OutputStream;
			HttpContext = ParentEngine.HttpContext;
			fileSystem.ClearFileAccess();
			this.eventRuntime.GetActiveEvents().Clear();
			foreach (var item in listOfDisposables)
			{
				item.Dispose();
			}
			if (reset)
			{
				CallbackInfos = null;
				var prParser = container.GetInstance<PrParser>();
				prParser.ClearVariables();
			}
			Name = string.Empty;
		}

		ConcurrentDictionary<string, EnginePool> enginePools = new();

		public EnginePool GetEnginePool(string rootPath)
		{
			rootPath = rootPath.TrimEnd(fileSystem.Path.DirectorySeparatorChar);
			if (enginePools.TryGetValue(rootPath, out var pool)) return pool;

			var tempContext = container.GetInstance<PLangAppContext>();

			pool = new EnginePool(2, () =>
			{

				using var serviceContainer = new ServiceContainer();
				
				serviceContainer.RegisterForPLang(rootPath, "/",
									container.GetInstance<IOutputStreamFactory>(), container.GetInstance<IOutputSystemStreamFactory>(),
									container.GetInstance<IErrorHandlerFactory>(), container.GetInstance<IErrorSystemHandlerFactory>(), this);

				var engine = serviceContainer.GetInstance<IEngine>();
				engine.Init(serviceContainer);
				engine.SetParentEngine(this);

				return engine;
			});

			enginePools.TryAdd(rootPath, pool);
			return pool;

		}
		
		public MemoryStack GetMemoryStack() => this.memoryStack;

		public IEventRuntime GetEventRuntime()
		{
			return this.eventRuntime;
		}

		public void SetCallingStep(GoalStep callingStep)
		{
			this.callingStep = callingStep;
		}
		public GoalStep? CallingStep => callingStep;

		public void AddContext(string key, object value)
		{
			if (ReservedKeywords.IsReserved(key))
			{
				throw new ReservedKeywordException($"{key} is reserved for the system. Choose a different name");
			}

			this.context.AddOrReplace(key, value);
		}
		public PLangAppContext GetContext() => this.context;


		public async Task<(object? Variables, IError? Error)> Run(string goalToRun)
		{
			AppContext.SetSwitch("Runtime", true);
			Goal goal = Goal.NotFound;

			// setup return variable
			List<object?> vars = new();
			ObjectValue ov = new ObjectValue("Run", vars);
			object? returnVars = null;
			IError? error = null;

			try
			{
				logger.LogInformation("App Start:" + DateTime.Now.ToLongTimeString());
				if (string.IsNullOrEmpty(goalToRun.Trim())) goalToRun = "Start.goal";

				error = eventRuntime.Load(false);
				if (error != null)
				{
					return (ov, error);
				}

				goal = GetStartGoal(goalToRun);
				if (goal == null)
				{
					(returnVars, error) = await eventRuntime.AppErrorEvents(new Error($"Goal '{goalToRun}' not found"));
					if (returnVars != null) vars.Add(returnVars);
					if (error != null) return (ov, error);
				}

				(returnVars, error) = await eventRuntime.RunStartEndEvents(EventType.Before, EventScope.StartOfApp, goal);
				if (returnVars != null) vars.Add(returnVars);
				if (error != null)
				{
					(returnVars, error) = await eventRuntime.AppErrorEvents(error);
					if (returnVars != null) vars.Add(returnVars);
					if (error != null) return (ov, error);
				}

				(returnVars, error) = await RunSetup(goal);
				if (returnVars != null) vars.Add(returnVars);
				if (error != null)
				{
					(returnVars, error) = await eventRuntime.AppErrorEvents(error);
					if (returnVars != null) vars.Add(returnVars);
					if (error != null) return (ov, error);
				}
				if (goalToRun.RemoveExtension().Equals("setup", StringComparison.OrdinalIgnoreCase)) return (ov, null);


				(returnVars, error) = await RunGoal(goal);
				if (returnVars != null) vars.Add(returnVars);
				if (error != null)
				{
					(returnVars, error) = await eventRuntime.AppErrorEvents(error);
					if (returnVars != null) vars.Add(returnVars);
					if (error != null) return (ov, error);
				}


				(returnVars, error) = await eventRuntime.RunStartEndEvents(EventType.After, EventScope.StartOfApp, goal);
				if (returnVars != null) vars.Add(returnVars);
				if (error != null)
				{
					(returnVars, error) = await eventRuntime.AppErrorEvents(error);
					if (returnVars != null) vars.Add(returnVars);
					if (error != null) return (ov, error);
				}

				WatchForRebuild();
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "OnStart");
				error = new Error(ex.Message, Exception: ex);
				return (ov, error);
			}
			finally
			{
				await KeepAlive();

				(returnVars, error) = await eventRuntime.RunStartEndEvents(EventType.Before, EventScope.EndOfApp, goal);
				if (returnVars != null) vars.Add(returnVars);
			}


			return (ov, error);
		}

		private async Task KeepAlive()
		{
			var alives = AppContext.GetData("KeepAlive") as List<Alive>;
			if (alives != null && alives.Count > 0)
			{
				logger.LogWarning("Keeping app alive, reasons:");
				foreach (var alive in alives)
				{
					logger.LogWarning(" - " + alive.Key);
				}

				while (alives != null && alives.Count > 0)
				{
					await Task.Delay(1000);
					alives = AppContext.GetData("KeepAlive") as List<Alive>;
					if (alives != null && alives.Count > 0)
					{
						var aliveTaskType = alives.FirstOrDefault(p => p.Key == "WaitForExecution");
						if (aliveTaskType?.Instances != null)
						{
							bool isCompleted = true;

							List<Task> tasks = new();
							for (int i = 0; i < aliveTaskType.Instances.Count; i++)
							{
								var engineWait = (EngineWait)aliveTaskType.Instances[i];
								tasks.Add(engineWait.task);

								if (engineWait.task.IsFaulted)
								{
									Console.WriteLine(engineWait.task.Exception.Flatten().ToString());
								}

								if (engineWait.task.IsCompleted)
								{
									aliveTaskType.Instances.Remove(engineWait);
									engineWait.engine.ParentEngine?.GetEnginePool(engineWait.engine.Path).Return(engineWait.engine);
									i--;
								}
							}
							if (aliveTaskType.Instances.Count == 0)
							{
								alives.Remove(aliveTaskType);
							}
						}
					}
				}
			}
		}

		private static CancellationTokenSource debounceTokenSource;
		private static readonly object debounceLock = new object();
		private IFileSystemWatcher? fileWatcher = null;

		public virtual void Dispose()
		{

			if (this.disposed)
			{
				return;
			}
			fileWatcher?.Dispose();
			memoryStack?.Clear();

			foreach (var item in listOfDisposables)
			{
				item.Dispose();
			}
			//context?.Clear();
			//_debugSemaphore.Dispose();


			this.disposed = true;
		}

		protected virtual void ThrowIfDisposed()
		{
			if (this.disposed)
			{
				throw new ObjectDisposedException(this.GetType().FullName);
			}
		}

		private void WatchForRebuild()
		{
			string path = fileSystem.Path.Join(fileSystem.BuildPath);
			if (fileWatcher != null) fileWatcher.Dispose();

			fileWatcher = fileSystem.FileSystemWatcher.New(path, "*.pr");

			fileWatcher.Changed += (object sender, FileSystemEventArgs e) =>
			{
				lock (debounceLock)
				{
					debounceTokenSource?.Cancel();
					debounceTokenSource?.Dispose();
					debounceTokenSource = new CancellationTokenSource();

					// Call the debounced method with a delay
					Task.Delay(1 * 1000, debounceTokenSource.Token)
						.ContinueWith(t =>
						{
							if (!t.IsCanceled)
							{
								Console.Write(".");
								prParser.ForceLoadAllGoals();
								var error = eventRuntime.Reload();
								if (error != null)
								{
									Console.WriteLine(error);
								}
								foreach (var pool in enginePools)
								{
									if (pool.Value == null) continue;

									pool.Value.ReloadGoals();
								}
							}
						}, TaskScheduler.Default);
				}
			};
			fileWatcher.IncludeSubdirectories = true;
			fileWatcher.EnableRaisingEvents = true;
		}


		private async Task HandleError(IError error)
		{
			if (error is IErrorHandled) return;

			logger.LogError(error.ToString());

			var appErrorEventResult = await eventRuntime.AppErrorEvents(error);
			if (appErrorEventResult.Error != null)
			{
				var me = new MultipleError(error, "Critical");
				me.Add(appErrorEventResult.Error);

				await container.GetInstance<IErrorHandlerFactory>().CreateHandler().ShowError(me);
			}
		}

		private async Task<(bool, IError?)> HandleFileAccessError(FileAccessRequestError fare)
		{

			var fileAccessHandler = container.GetInstance<IFileAccessHandler>();
		
			(var answer, var error) = await AskUser.GetAnswer(this, fare.Message);
			if (error != null) return (false, error);

			return await fileAccessHandler.ValidatePathResponse(fare.AppName, fare.Path, answer.ToString(), FileSystem.Id);
			
		}


		private async Task<(object? Variables, IError?)> RunSetup(Goal startGoal)
		{
			var setupGoals = prParser.GetAllGoals().Where(p => p.IsSetup);
			if (!setupGoals.Any())
			{
				return (null, null);
			}

			logger.LogDebug("Setup");


			List<object?> vars = new();
			var ov = new ObjectValue("Setup", vars);

			foreach (var goal in setupGoals)
			{
				goal.AddVariables(startGoal.GetVariables());
				if (goal.DataSourceName != null && goal.DataSourceName.Contains("%")) continue;

				var result = await RunGoal(goal);
				if (result.Variables != null)
				{
					vars.Add(result.Variables);
				}
				if (result.Error != null) return (ov, result.Error);
			}
			return (ov, null);
		}


		private void SetStepLogLevel(GoalStep step)
		{
			if (string.IsNullOrEmpty(step.LoggerLevel)) return;

			Enum.TryParse(step.LoggerLevel, true, out Microsoft.Extensions.Logging.LogLevel logLevel);
			AppContext.SetData("StepLogLevelByUser", logLevel);
		}

		private void ResetStepLogLevel(Goal goal)
		{
			if (goal.Comment == null && !goal.GoalName.Contains("[")) return;

			string comment = (goal.Comment ?? string.Empty).ToLower();
			string goalName = goal.GoalName.ToLower();

			string[] logLevels = ["trace", "debug", "information", "warning", "error"];
			foreach (var logLevel in logLevels)
			{
				if (comment.Contains($"[{logLevel}]") || goalName.Contains($"[{logLevel}]"))
				{
					AppContext.SetData("GoalLogLevelByUser", Microsoft.Extensions.Logging.LogLevel.Trace);
					return;
				}
			}
			return;
		}
		public async Task<(object? Variables, IError? Error)> RunGoal(GoalToCallInfo goalToCall, Goal parentGoal, uint waitForXMillisecondsBeforeRunningGoal = 0)
		{
			if (parentGoal == null) return (null, new ProgramError("Parent goal cannot be empty"));

			var (goal, error) = PrParser.GetGoal(goalToCall);
			if (error != null) return (null, error);

			foreach (var parameter in goalToCall.Parameters)
			{
				memoryStack.Put(parameter.Key, parameter.Value);
			}
			goal!.ParentGoal = parentGoal;
			
			return await RunGoal(goal, waitForXMillisecondsBeforeRunningGoal);
		}
		public async Task<(object? Variables, IError? Error)> RunGoal(Goal goal, uint waitForXMillisecondsBeforeRunningGoal = 0)
		{
			if (waitForXMillisecondsBeforeRunningGoal > 0) await Task.Delay((int)waitForXMillisecondsBeforeRunningGoal);
			goal.Stopwatch = Stopwatch.StartNew();
			goal.UniqueId = Guid.NewGuid().ToString();

			logger.LogDebug($"Goal {goal.GoalName}");

			AppContext.SetSwitch("Runtime", true);
			SetLogLevel(goal.Comment);

			int stepIndex = -1;
			try
			{
				logger.LogTrace("RootDirectory:{0}", fileSystem.RootDirectory);
				foreach (var injection in goal.Injections)
				{
					container.RegisterForPLangUserInjections(injection.Type, injection.Path, injection.IsGlobal, injection.EnvironmentVariable, injection.EnvironmentVariableValue);
				}

				logger.LogDebug($" - Running Before event on goal - {goal.Stopwatch.ElapsedMilliseconds}");

				var result = await eventRuntime.RunGoalEvents(EventType.Before, goal);
				if (result.Error != null) return result;

				logger.LogDebug($" - Event done, now running Steps - {goal.Stopwatch.ElapsedMilliseconds}");

				//if (await CachedGoal(goal)) return null;
				(var returnValues, stepIndex, var stepError) = await RunSteps(goal, 0);
				
				await DisposeGoal(goal);

				if (stepError != null && stepError is not IErrorHandled) return (returnValues, stepError);
				//await CacheGoal(goal);
				logger.LogDebug($" - Steps done, running After events - {goal.Stopwatch.ElapsedMilliseconds}");

				result = await eventRuntime.RunGoalEvents(EventType.After, goal);
				if (result.Error != null) return result;

				logger.LogDebug($" - After events done - {goal.Stopwatch.ElapsedMilliseconds}");

				return (returnValues, stepError);

			}
			catch (Exception ex)
			{
				var error = new Error(ex.Message, Exception: ex);
				//if (context.ContainsKey(ReservedKeywords.IsEvent)) return error;

				var eventError = await HandleGoalError(error, goal, stepIndex);
				return eventError;
			}
			finally
			{
				AppContext.SetData("GoalLogLevelByUser", null);

				var os = OutputStreamFactory.CreateHandler();
				if (os is UIOutputStream)
				{
					if (goal.ParentGoal == null)
					{
						((UIOutputStream)os).Flush();
					}
				}
				goal.Stopwatch.Stop();
				logger.LogDebug($"=> Total time for {goal.GoalName} - " + goal.Stopwatch.ElapsedMilliseconds);

			}

		}

		private async Task DisposeGoal(Goal goal)
		{
			
		}

		private async Task<(object? ReturnValue, int StepIndex, IError? Error)> RunSteps(Goal goal, int stepIndex = 0)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			object? returnValues = null;
			IError? error = null;
			logger.LogDebug($"  - Goal {goal.GoalName} starts - {stopwatch.ElapsedMilliseconds}");
			for (; stepIndex < goal.GoalSteps.Count; stepIndex++)
			{
				Stopwatch stepWatch = Stopwatch.StartNew();
				logger.LogDebug($"   - Step idx {stepIndex} starts - {stepWatch.ElapsedMilliseconds}");
				if (CallbackInfos != null)
				{
					var callbackInfo = CallbackInfos.FirstOrDefault(p => p.GoalHash == goal.Hash);
					if (callbackInfo != null && stepIndex < callbackInfo.StepIndex)
					{
						continue;
					}
					else if (callbackInfo != null && stepIndex == callbackInfo.StepIndex)
					{
						goal.GoalSteps[stepIndex].Callback = callbackInfo;
					}
					logger.LogDebug($"   - Step has callback info - {stepWatch.ElapsedMilliseconds}");
				}
				(returnValues, error) = await RunStep(goal, stepIndex);
				if (error != null)
				{
					logger.LogDebug($"   - Step idx {stepIndex} has ERROR - {stepWatch.ElapsedMilliseconds}");
					if (error is MultipleError me && me.IsErrorHandled)
					{
						var hasEndGoal = FindErrorHandled(me);
						if (hasEndGoal != null) error = hasEndGoal;
					}
					if (error is EndGoal endGoal)
					{
						logger.LogDebug($"   - Exiting goal because of end goal: {endGoal.Goal?.RelativeGoalPath} - {stepWatch.ElapsedMilliseconds}");
						try
						{
							if (!endGoal.EndingGoal?.RelativePrPath.Equals(goal.RelativePrPath) == true)
							{
								logger.LogDebug($"   - End goal doing return: {endGoal.Goal?.RelativeGoalPath} - {stepWatch.ElapsedMilliseconds}");
								return (returnValues, stepIndex, endGoal);
							}
							else if (endGoal.EndingGoal == null)
							{
								logger.LogError("Ending goal is null, this should not happen:" + JsonConvert.SerializeObject(endGoal));
							}
						}
						catch (Exception ex)
						{
							Console.WriteLine(ex);
							Console.WriteLine("endGoal.EndingGoal:" + JsonConvert.SerializeObject(endGoal.EndingGoal));
							Console.WriteLine("goal:" + JsonConvert.SerializeObject(goal));

						}

							logger.LogDebug($"   - End goal doing continue: {endGoal.Goal?.RelativeGoalPath} - {stepWatch.ElapsedMilliseconds}");
						return (returnValues, stepIndex, null);
					}
					var errorInGoalErrorHandler = await HandleGoalError(error, goal, stepIndex);
					if (errorInGoalErrorHandler.Error != null) return (returnValues, stepIndex, errorInGoalErrorHandler.Error);
					if (errorInGoalErrorHandler.Error is IErrorHandled) error = null;
				}
				logger.LogDebug($"   - Step idx {stepIndex} done - {stepWatch.ElapsedMilliseconds} - Current for all steps: {stopwatch.ElapsedMilliseconds}");
			}
			logger.LogDebug($"  - All steps done in {goal.GoalName} - {stopwatch.ElapsedMilliseconds}");
			return (returnValues, stepIndex, error);
		}



		private IError? FindErrorHandled(MultipleError me)
		{
			var hasEndGoal = me.ErrorChain.FirstOrDefault(p => p is IErrorHandled);
			if (hasEndGoal != null) return hasEndGoal;
			var handledEventError = me.ErrorChain.FirstOrDefault(p => p is HandledEventError) as HandledEventError;
			if (handledEventError != null)
			{
				if (handledEventError.InitialError is IErrorHandled) return handledEventError.InitialError;
			}
			return null;
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

		private async Task<(object? Variables, IError? Error)> HandleGoalError(IError error, Goal goal, int goalStepIndex)
		{
			if (error.Goal == null) error.Goal = goal;
			if (error.Step == null && goal != null && goal.GoalSteps.Count > goalStepIndex - 1 && goalStepIndex > -1) error.Step = goal.GoalSteps[goalStepIndex];
			if (error is IErrorHandled || error is IUserInputError) return (null, error);

			var eventError = await eventRuntime.RunGoalErrorEvents(goal, goalStepIndex, error);
			return eventError;
		}

		public async Task<(object? ReturnValue, IError? Error)> RunFromStep(string prFile)
		{
			var goalPath = fileSystem.Path.GetDirectoryName(prFile);
			var goalFile = fileSystem.Path.Join(goalPath, ISettings.GoalFileName);

			var goal = prParser.ParsePrFile(goalFile);
			var step = goal.GoalSteps.FirstOrDefault(p => p.AbsolutePrFilePath == prFile);

			var result = await RunSteps(goal, step.Index);
			return (result.ReturnValue, result.Error);
		}

		private async Task<(object? ReturnValue, IError? Error)> RunStep(Goal goal, int goalStepIndex, int retryCount = 0)
		{

			var step = goal.GoalSteps[goalStepIndex];
			goal.CurrentStepIndex = goalStepIndex;
			step.Stopwatch = Stopwatch.StartNew();

			SetStepLogLevel(step);
			try
			{
				if (HasExecuted(step)) return (null, null);

				step.UniqueId = Guid.NewGuid().ToString();
				logger.LogDebug($"     - Load instruction for step: {step.Text.MaxLength(20)} - {step.Stopwatch.ElapsedMilliseconds}");

				var error = LoadInstruction(goal, step);
				if (error != null)
				{
					return (null, error);
				}
				logger.LogDebug($"     - Have instruction - {step.Stopwatch.ElapsedMilliseconds}");
				if (retryCount == 0)
				{
					logger.LogDebug($"     - Before event starts - {step.Stopwatch.ElapsedMilliseconds}");
					// Only run event one time, even if step is tried multiple times
					var stepEventResult = await eventRuntime.RunStepEvents(EventType.Before, goal, step);
					if (stepEventResult.Error != null) return (null, stepEventResult.Error);
				}

				logger.LogDebug($"     - ProcessPrFile {step.PrFileName} - {step.Stopwatch.ElapsedMilliseconds}");

				var result = await ProcessPrFile(goal, step, goalStepIndex);

				if (result.Error != null)
				{
					result = await HandleStepError(goal, step, goalStepIndex, result.Error, retryCount);
					if (result.Error != null && result.Error is MultipleError me && !me.IsErrorHandled) return result;
				}
				logger.LogDebug($"     - Done with ProcessPrFile, doing after events - {step.Stopwatch.ElapsedMilliseconds}");

				if (retryCount == 0)
				{
					// Only run event one time, even if step is tried multiple times, retryCount is 0 event after retry when callstack is traversed back up
					var stepEventResult = await eventRuntime.RunStepEvents(EventType.After, goal, step);
					if (stepEventResult.Error != null) return stepEventResult;
				}

				logger.LogDebug($"     - Done with after events - {step.Stopwatch.ElapsedMilliseconds}");
				return result;
			}
			catch (Exception stepException)
			{

				var error = new ExceptionError(stepException, stepException.Message, goal, step);
				var result = await HandleStepError(goal, step, goalStepIndex, error, retryCount);

				return result;
			}
			finally
			{
				logger.LogDebug($"     - Reset log level - {step.Stopwatch.ElapsedMilliseconds}");
				ResetStepLogLevel(goal);
				step.Stopwatch.Stop();
				logger.LogDebug($"     - Step all done - {step.Stopwatch.ElapsedMilliseconds}");
			}

		}



		private async Task<(object? ReturnValue, IError? Error)> HandleStepError(Goal goal, GoalStep step, int goalStepIndex, IError? error, int retryCount)
		{
			if (error == null || error is IErrorHandled || error is EndGoal || error is IUserInputError) return (null, error);
			if (step.ModuleType == "PLang.Modules.ThrowErrorModule" && step.Instruction?.Function.Name == "Throw") return (null, error);

			if (error is Errors.AskUser.AskUserError aue)
			{
				var (answer, answerError) = await AskUser.GetAnswer(this, aue.Message);
				if (answerError != null) return (null, answerError);

				var (_, callbackError) = await aue.InvokeCallback([answer]);
				if (callbackError != null) return (null, callbackError);

				return await RunStep(goal, goalStepIndex);
			}

			if (error is FileAccessRequestError fare)
			{
				(var isHandled, var handlerError) = await HandleFileAccessError(fare);
				if (handlerError != null)
				{
					return (null, ErrorHelper.GetMultipleError(error, handlerError));
				}

				return await RunStep(goal, goalStepIndex);

			}

			var errorHandler = StepHelper.GetErrorHandlerForStep(step.ErrorHandlers, error);
			if (errorHandler != null)
			{

				if (HasRetriedToRetryLimit(errorHandler, retryCount)) return (null, error);

				if (ShouldRunRetry(errorHandler, true))
				{
					logger.LogDebug($"Error occurred - Before goal to call - Will retry in {errorHandler.RetryHandler?.RetryDelayInMilliseconds}ms. Attempt nr. {retryCount} of {errorHandler.RetryHandler?.RetryCount}\nError:{error}");
					if (errorHandler.RetryHandler?.RetryDelayInMilliseconds != null && errorHandler.RetryHandler.RetryDelayInMilliseconds > 0)
					{
						await Task.Delay(errorHandler.RetryHandler.RetryDelayInMilliseconds.Value);
					}
					return await RunStep(goal, goalStepIndex, ++retryCount);
				}
			}

			var eventRuntime = container.GetInstance<IEventRuntime>();
			var stepErrorResult = await eventRuntime.RunOnErrorStepEvents(error, goal, step);
			if (stepErrorResult.Error != null)
			{
				return stepErrorResult;
			}

			// step.Retry can be step by a goal in RunOnErrorStepEvents
			if (step.Retry || ShouldRunRetry(errorHandler, false))
			{
				// reset the retry property
				step.Retry = false;

				logger.LogDebug($"Error occurred - After goal to call - Will retry in {errorHandler?.RetryHandler?.RetryDelayInMilliseconds}ms. Attempt nr. {retryCount} of {errorHandler?.RetryHandler?.RetryCount}\nError:{error}");
				if (errorHandler?.RetryHandler?.RetryDelayInMilliseconds != null && errorHandler.RetryHandler.RetryDelayInMilliseconds > 0)
				{
					await Task.Delay(errorHandler.RetryHandler.RetryDelayInMilliseconds.Value);
				}
				return await RunStep(goal, goalStepIndex, ++retryCount);
			}

			return (null, stepErrorResult.Error);
		}

		private bool HasRetriedToRetryLimit(ErrorHandler? errorHandler, int retryCount)
		{
			if (retryCount == 0) return false;
			if (errorHandler == null || errorHandler.RetryHandler == null) return false;
			return (errorHandler.RetryHandler.RetryCount <= retryCount);
		}

		private bool ShouldRunRetry(ErrorHandler? errorHandler, bool isBefore)
		{
			return (errorHandler != null && errorHandler.RunRetryBeforeCallingGoalToCall == isBefore &&
					errorHandler.RetryHandler != null);
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


		public async Task<(object? ReturnValue, IError? Error)> ProcessPrFile(Goal goal, GoalStep step, int stepIndex)
		{
			if (stepIndex < goal.GoalSteps.Count && !goal.GoalSteps[stepIndex].Execute)
			{
				logger.LogDebug($"Step is disabled: {goal.GoalSteps[stepIndex].Execute}");
				return (null, null);
			}
			if (step.Stopwatch == null) step.Stopwatch = Stopwatch.StartNew();

			logger.LogDebug($"     - Get runtime type {step.ModuleType} - {step.Stopwatch.ElapsedMilliseconds}");

			Type? classType = typeHelper.GetRuntimeType(step.ModuleType);
			if (classType == null)
			{
				return (null, new StepError("Could not find module:" + step.ModuleType, step, Key: "ModuleNotFound"));
			}

			if (step.Instruction == null) LoadInstruction(goal, step);

			goal.AddVariable(goal, variableName: ReservedKeywords.Goal);
			goal.AddVariable(step, variableName: ReservedKeywords.Step);
			goal.AddVariable(step.Instruction, variableName: ReservedKeywords.Instruction);

			logger.LogDebug($"     - Getting instace {step.ModuleType} - {step.Stopwatch.ElapsedMilliseconds}");

			BaseProgram? classInstance;
			try
			{
				classInstance = container.GetInstance(classType) as BaseProgram;
				if (classInstance == null)
				{
					return (null, new Error($"Could not create instance of {classType}", Key: "InstanceNotCreated"));
				}
			}
			catch (MissingSettingsException mse)
			{
				var engine = container.GetInstance<IEngine>();
				var error = await MissingSettingsHelper.HandleMissingSetting(engine, mse);
				if (error != null) return (null, error);

				return await ProcessPrFile(goal, step, stepIndex);
			}
			logger.LogDebug($"     - Init instance {step.ModuleType} - {step.Stopwatch.ElapsedMilliseconds}");
			classInstance.Init(container, goal, step, step.Instruction, this.HttpContext);

			if (classInstance is IAsyncConstructor asyncConstructor)
			{
				logger.LogDebug($"     - Calling async init on instance {step.ModuleType} - {step.Stopwatch.ElapsedMilliseconds}");
				await asyncConstructor.AsyncConstructor();
			}

			using (var cts = new CancellationTokenSource())
			{
				long executionTimeout = (step.CancellationHandler == null) ? 30 * 1000 : step.CancellationHandler.CancelExecutionAfterXMilliseconds ?? 30 * 1000;
				cts.CancelAfter(TimeSpan.FromMilliseconds(executionTimeout));

				try
				{
					if (classInstance is IDisposable disposable)
					{
						listOfDisposables.Add(disposable);
					}
					logger.LogDebug($"     - Calling Run instance {step.ModuleType} - {step.Stopwatch.ElapsedMilliseconds}");

					var task = classInstance.Run();
					await task;

					var result = task.Result;
					if (result.Error != null)
					{
						if (result.Error.Step == null) result.Error.Step = step;
						return result;
					}
					if (step.RunOnce)
					{
						var dict = settings.GetOrDefault<Dictionary<string, DateTime>>(typeof(Engine), "SetupRunOnce", new());
						if (dict == null) dict = new();

						dict.TryAdd(step.RelativePrPath, DateTime.UtcNow);
						settings.Set<Dictionary<string, DateTime>>(typeof(Engine), "SetupRunOnce", dict);
					}

					logger.LogDebug($"     - Done running instance {step.ModuleType} - {step.Stopwatch.ElapsedMilliseconds}");

					return result;
				}
				catch (OperationCanceledException)
				{
					if (step.CancellationHandler?.GoalNameToCallAfterCancellation != null)
					{
						var engine = container.GetInstance<IEngine>();
						var pseudoRuntime = container.GetInstance<IPseudoRuntime>();

						var result = await pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath, step.CancellationHandler.GoalNameToCallAfterCancellation, goal);
						return (null, result.error);
					}
					return (null, new StepError("Step was cancelled because it ran for to long. To extend the timeout, include timeout in your step.", step));
				}
				catch (Exception ex)
				{
					return (null, new StepError(ex.Message, step, "StepException", 500, Exception: ex));
				}
				finally
				{
					// reset the Execute to false on all steps inside if statement
					if (step.Indent > 0)
					{
						step.Execute = false;
					}
				}
			}
		}

		private IError? LoadInstruction(Goal goal, GoalStep step)
		{
			var instruction = prParser.ParseInstructionFile(step);
			if (instruction == null)
			{
				return new StepError($"Instruction file could not be loaded for {step.RelativePrPath}", step, Key: "InstructionFileNotLoaded");
			}
			step.Instruction = instruction;
			return null;
		}

		private List<IDisposable> listOfDisposables = new();
		
		private Goal? GetStartGoal(string goalName)
		{
			string prPath = fileSystem.Path.Join(goalName.Replace(".goal", ""), ISettings.GoalFileName);
			string absolutePath = fileSystem.Path.Join(fileSystem.BuildPath, prPath);
			
			return prParser.GetGoal(absolutePath);
			
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

					var folderPath = goalFiles.FirstOrDefault(p => p.ToLower() == fileSystem.Path.Join(fileSystem.RootDirectory, ".build", name, ISettings.GoalFileName).ToLower());
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

			if (!fileSystem.Directory.Exists(fileSystem.Path.Join(fileSystem.BuildPath, "Start")))
			{
				return new();
			}

			var startFile = fileSystem.Directory.GetFiles(fileSystem.Path.Join(fileSystem.BuildPath, "Start"), ISettings.GoalFileName, SearchOption.AllDirectories).FirstOrDefault();
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
			var goal = prParser.GetGoalByAppAndGoalName(fileSystem.RootDirectory, goalName, callingGoal);
			if (goal != null) return goal;

			return prParser.GetGoalByAppAndGoalName(fileSystem.SystemDirectory, goalName, callingGoal);
		}



		public List<Goal> GetGoalsAvailable(string appPath, string goalName)
		{
			return prParser.GetAllGoals();
		}


	}
}
