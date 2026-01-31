using LightInject;
using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using System.Collections;
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
using PLang.Services.OutputStream;
using PLang.Services.OutputStream.Sinks;
using PLang.Utils;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Abstractions;
using static PLang.Runtime.PseudoRuntime;
using static PLang.Utils.StepHelper;

namespace PLang.Runtime
{
	public interface IEngine : IDisposable
	{
		string Id { get; init; }
		string Name { get; set; }
		string Path { get; }
		DateTime LastAccess { get; set; }

		static readonly string DefaultEnvironment = "production";
		string Environment { get; set; }
		IPLangFileSystem FileSystem { get; }
		IPrParser PrParser { get; }
		ConcurrentDictionary<string, Engine.LiveConnection> LiveConnections { get; set; }

		IServiceContainer Container { get; }
		IAppCache AppCache { get; }
		IOutputSink UserSink { get; set; }
		IOutputSink SystemSink { get; set; }
		ResolveEventHandler AsmHandler { get; set; }
		List<ISerializer> Serializers { get; set; }
		PLangContext Context { get; }

		/// <summary>
		/// Module registry for accessing and controlling modules.
		/// Returns the current context's registry if available, otherwise the default registry.
		/// </summary>
		IModuleRegistry Modules { get; }

		/// <summary>
		/// Creates a clone of the default module registry for a new context.
		/// </summary>
		ModuleRegistry CloneDefaultModuleRegistry();

		void AddContext(string key, object value);
		PLangAppContext GetAppContext();
		void Init(IServiceContainer container);
		Task<(object? Variables, IError? Error)> Run(string goalToRun, PLangContext context);
		Task<(object? Variables, IError? Error)> RunGoal(Goal goal, PLangContext context, uint waitForXMillisecondsBeforeRunningGoal = 0);
		Task<(object? Variables, IError? Error)> RunGoal(GoalToCallInfo goalToCall, Goal parentGoal, PLangContext context, uint waitForXMillisecondsBeforeRunningGoal = 0);
		Goal? GetGoal(string goalName, Goal? callingGoal = null);
		Task<(object? ReturnValue, IError? Error)> RunFromStep(string prFile, PLangContext context);
		Task<(object? ReturnValue, IError? Error)> ProcessPrFile(Goal goal, GoalStep goalStep, int stepIndex, PLangContext context);
		IEventRuntime GetEventRuntime();
		void Reset(bool reset = false);
		void ReloadGoals();
		T GetProgram<T>() where T : BaseProgram;
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
		private IPLangContextAccessor contextAccessor;
		/// <summary>
		/// Returns context's system sink if available, otherwise the default.
		/// </summary>
		public IOutputSink SystemSink
		{
			get => contextAccessor?.Current?.SystemSink ?? _defaultSystemSink;
			set
			{
				if (contextAccessor?.Current != null)
					contextAccessor.Current.SystemSink = value;
				else
					_defaultSystemSink = value;
			}
		}

		/// <summary>
		/// Returns context's user sink if available, otherwise the default.
		/// </summary>
		public IOutputSink UserSink
		{
			get => contextAccessor?.Current?.UserSink ?? _defaultUserSink;
			set
			{
				if (contextAccessor?.Current != null)
					contextAccessor.Current.UserSink = value;
				else
					_defaultUserSink = value;
			}
		}
		public List<ISerializer> Serializers { get; set; }
		private IPrParser prParser;
		private PLangAppContext appContext;
		private ModuleRegistry _defaultModuleRegistry;
		private IOutputSink _defaultUserSink;
		private IOutputSink _defaultSystemSink;
		public IPrParser PrParser { get { return prParser; } }
		public DateTime LastAccess { get; set; }

		/// <summary>
		/// Returns context's module registry if available, otherwise the default.
		/// </summary>
		public IModuleRegistry Modules => contextAccessor?.Current?.Modules ?? _defaultModuleRegistry;

		/// <summary>
		/// Creates a clone of the default module registry for a new context.
		/// </summary>
		public ModuleRegistry CloneDefaultModuleRegistry() => _defaultModuleRegistry.Clone();

		public IAppCache AppCache
		{
			get
			{
				return container.GetInstance<IAppCache>();
			}
		}

		public PLangContext Context
		{
			get
			{
				return contextAccessor.Current;
			}
		}

		public IServiceContainer Container { get { return this.container; } }
		public List<IEngine> ChildEngines { get; set; } = new();
		public ConcurrentDictionary<string, LiveConnection> LiveConnections { get; set; } = new();
		public record LiveConnection(Microsoft.AspNetCore.Http.HttpResponse Response, bool IsFlushed)
		{
			public DateTime Created { get; set; } = DateTime.Now;
			public DateTime Updated { get; set; } = DateTime.Now;
			public bool IsFlushed { get; set; } = IsFlushed;
		};

		public IPLangFileSystem FileSystem { get { return fileSystem; } }
		public ResolveEventHandler AsmHandler { get; set; }
		public Engine()
		{
			Id = Guid.NewGuid().ToString();

			AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
			{
				Console.WriteLine($"Unhandled exception: {args.ExceptionObject}");
			};
			LastAccess = DateTime.Now;
			Serializers = new();

		}

		public void Init(IServiceContainer container)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			this.container = container;
			this.appContext = container.GetInstance<PLangAppContext>();
			this.fileSystem = container.GetInstance<IPLangFileSystem>();
			this.identityService = container.GetInstance<IPLangIdentityService>();
			this.logger = container.GetInstance<ILogger>();
			this.settings = container.GetInstance<ISettings>();
			this.eventRuntime = container.GetInstance<IEventRuntime>();
			//this.eventRuntime.SetContainer(container, context);
			this.eventRuntime.Load();
			logger.LogDebug($" ---------- Init on Engine  ---------- {stopwatch.ElapsedMilliseconds}");
			this.typeHelper = container.GetInstance<ITypeHelper>();
			this.contextAccessor = container.GetInstance<IPLangContextAccessor>();

			// Initialize default ModuleRegistry and register all modules
			this._defaultModuleRegistry = new ModuleRegistry(container, contextAccessor);
			this._defaultModuleRegistry.RegisterAllFromContainer();
			logger.LogDebug($" ---------- ModuleRegistry initialized  ---------- {stopwatch.ElapsedMilliseconds}");

			this.prParser = container.GetInstance<IPrParser>();
			var fileSystem = container.GetInstance<IPLangFileSystem>();
			var plangGlobal = new Dictionary<string, object>()
			{
				{ "osPath", fileSystem.SystemDirectory },
				{ "rootPath", fileSystem.RootDirectory },
				{ "EngineUniqueId", Id}
			};
			this.appContext.AddOrReplace("!plang", plangGlobal);

			this.appContext.AddOrReplace(ReservedKeywords.MyIdentity, identityService.GetCurrentIdentity());
			logger.LogDebug($" ---------- Done Init on Engine  ---------- {stopwatch.ElapsedMilliseconds}");
		}




		public void Reset(bool reset = false)
		{
			LastAccess = DateTime.UtcNow;

			if (fileSystem != null)
			{
				fileSystem.ClearFileAccess();
			}

			contextAccessor.Current = null;
			var msa = container.GetInstance<IMemoryStackAccessor>();
			msa.Current = null;
		}

		public IEventRuntime GetEventRuntime()
		{
			return this.eventRuntime;
		}

		public T GetProgram<T>() where T : BaseProgram
		{
			var program = container.GetInstance<T>();

			var context = contextAccessor.Current;
			program.Init(container, context.CallStack.CurrentGoal, context.CallStack.CurrentStep, context.CallStack.CurrentStep.Instruction, contextAccessor);
			return program;
		}

		public void AddContext(string key, object value)
		{
			if (ReservedKeywords.IsReserved(key))
			{
				throw new ReservedKeywordException($"{key} is reserved for the system. Choose a different name");
			}

			this.appContext.AddOrReplace(key, value);
		}
		public PLangAppContext GetAppContext() => this.appContext;


		public async Task<(object? Variables, IError? Error)> Run(string goalToRun, PLangContext context)
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

				(returnVars, error) = await RunSetup(goal, context);
				if (returnVars != null) vars.Add(returnVars);
				if (error != null)
				{
					(returnVars, error) = await eventRuntime.AppErrorEvents(error);
					if (returnVars != null) vars.Add(returnVars);
					if (error != null) return (ov, error);
				}
				if (goalToRun.RemoveExtension().Equals("setup", StringComparison.OrdinalIgnoreCase)) return (ov, null);


				(returnVars, error) = await RunGoal(goal, context);
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
			/*
			foreach (var listOfDisp in listOfDisposables)
			{
				foreach (var item in listOfDisp.Value)
				{
					item.Dispose();
				}
			}*/

			foreach (var child in ChildEngines)
			{
				child.Container.Dispose();
			}

			AppDomain.CurrentDomain.AssemblyResolve -= AsmHandler;
			AsmHandler = null;

			this.prParser = null;
			this.container = null;
			this.fileSystem = null;
			this.identityService = null;
			this.logger = null;
			this.settings = null;
			this.eventRuntime = null;
			this.typeHelper = null;
			this.contextAccessor = null;
			this.appContext = null;
			//container.Dispose();

			this.disposed = true;
		}

		protected virtual void ThrowIfDisposed()
		{
			if (this.disposed)
			{
				throw new ObjectDisposedException(this.GetType().FullName);
			}
		}



		private async Task<(bool, IError?)> HandleFileAccessError(FileAccessRequestError fare, PLangContext context)
		{

			var fileAccessHandler = container.GetInstance<IFileAccessHandler>();

			(var answer, var error) = await AskUser.GetAnswer(this, context, fare.Message);
			if (error != null) return (false, error);

			return await fileAccessHandler.ValidatePathResponse(fare.AppName, fare.Path, answer.ToString(), FileSystem.Id);

		}


		private async Task<(object? Variables, IError?)> RunSetup(Goal startGoal, PLangContext context)
		{
			var setupGoals = prParser.GetAllGoals().Where(p => p.IsSetup).OrderBy(p => !p.GoalName.Equals("Setup", StringComparison.OrdinalIgnoreCase)); //Setup should come first
			if (!setupGoals.Any())
			{
				return (null, null);
			}

			logger.LogDebug("Setup");


			List<object?> vars = new();
			var ov = new ObjectValue("Setup", vars);

			foreach (var goal in setupGoals)
			{
				if (goal.DataSourceName != null && goal.DataSourceName.Contains("%")) continue;

				var result = await RunGoal(goal, context);
				if (result.Variables != null)
				{
					vars.Add(result.Variables);
				}
				if (result.Error != null) return (ov, result.Error);

				var dict = settings.GetOrDefault<Dictionary<string, DateTime>>(typeof(Engine), "SetupRunOnce", new());
				if (dict == null) dict = new();

				foreach (var step in goal.GoalSteps)
				{
					if (step.Executed == null) return (null, new Error($"{step.RelativeGoalPath} was not executed"));
					dict.TryAdd(step.RelativePrPath, step.Executed.Value);
				}

				settings.Set<Dictionary<string, DateTime>>(typeof(Engine), "SetupRunOnce", dict);
			}
			return (ov, null);
		}


		public async Task<(object? Variables, IError? Error)> RunGoal(GoalToCallInfo goalToCall, Goal parentGoal, PLangContext context, uint waitForXMillisecondsBeforeRunningGoal = 0)
		{
			if (parentGoal == null) return (null, new ProgramError("Parent goal cannot be empty"));

			var (goal, error) = PrParser.GetGoal(goalToCall);
			if (error != null) return (null, error);

			foreach (var parameter in goalToCall.Parameters)
			{
				context.MemoryStack.Put(parameter.Key, parameter.Value);
			}
			goal!.ParentGoal = parentGoal;

			return await RunGoal(goal, context, waitForXMillisecondsBeforeRunningGoal);
		}
		public async Task<(object? Variables, IError? Error)> RunGoal(Goal goal, PLangContext context, uint waitForXMillisecondsBeforeRunningGoal = 0)
		{
			return await RunGoal(goal, new(), context, waitForXMillisecondsBeforeRunningGoal);
		}
		public async Task<(object? Variables, IError? Error)> RunGoal(Goal goal, Dictionary<string, object?> Parameters, PLangContext context, uint waitForXMillisecondsBeforeRunningGoal = 0)
		{
			return await RunGoalInternal(goal, context, waitForXMillisecondsBeforeRunningGoal);
		}

		private async Task<(object? Variables, IError? Error)> HandleGoalError(IError error, Goal goal, int goalStepIndex, PLangContext context)
		{
			if (error.Goal == null) error.Goal = goal;
			if (error.Step == null && goal != null && goal.GoalSteps.Count > goalStepIndex - 1 && goalStepIndex > -1) error.Step = goal.GoalSteps[goalStepIndex];
			if (error is IErrorHandled || error is IUserInputError) return (null, error);

			var eventError = await eventRuntime.RunGoalErrorEvents(goal, goalStepIndex, error);
			return eventError;
		}

		public async Task<(object? ReturnValue, IError? Error)> RunFromStep(string prFile, PLangContext context)
		{
			var goalPath = fileSystem.Path.GetDirectoryName(prFile);
			var goalFile = fileSystem.Path.Join(goalPath, ISettings.GoalFileName);

			var goal = prParser.ParsePrFile(goalFile);
			var step = goal.GoalSteps.FirstOrDefault(p => p.AbsolutePrFilePath == prFile);

			return await RunGoalInternal(goal, context, 0, step.Index);
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


		public async Task<(object? ReturnValue, IError? Error)> ProcessPrFile(Goal goal, GoalStep step, int stepIndex, PLangContext context)
		{
			if (stepIndex < goal.GoalSteps.Count && !goal.GoalSteps[stepIndex].Execute)
			{
				logger.LogDebug($"Step is disabled: {goal.GoalSteps[stepIndex].Execute}");
				return (null, null);
			}
			if (step.Stopwatch == null) step.Stopwatch = Stopwatch.StartNew();

			logger.LogTrace($"     - Get runtime type {step.ModuleType} - {step.Stopwatch.ElapsedMilliseconds}");

			Type? classType = typeHelper.GetRuntimeType(step.ModuleType);
			if (classType == null)
			{
				return (null, new StepError("Could not find module:" + step.ModuleType, step, Key: "ModuleNotFound"));
			}

			if (step.Instruction == null) LoadInstruction(goal, step);
			
			context.AddVariable(goal, variableName: ReservedKeywords.Goal);
			context.AddVariable(step, variableName: ReservedKeywords.Step);
			context.AddVariable(step.Instruction, variableName: ReservedKeywords.Instruction);

			logger.LogTrace($"     - Getting instance {step.ModuleType} - {step.Stopwatch.ElapsedMilliseconds}");

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
				var error = await MissingSettingsHelper.HandleMissingSetting(this, context, mse);
				if (error != null) return (null, error);

				return await ProcessPrFile(goal, step, stepIndex, context);
			}
			logger.LogDebug($"     - Init instance {step.ModuleType} - {step.Stopwatch.ElapsedMilliseconds}");
			classInstance.Init(container, goal, step, step.Instruction, contextAccessor);

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
						context.CallStack.AddDisposable(disposable);
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

					if (step.RunOnce && step.Executed == null)
					{
						step.Executed = DateTime.UtcNow;
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

						var result = await pseudoRuntime.RunGoal(engine, contextAccessor, goal.RelativeAppStartupFolderPath, step.CancellationHandler.GoalNameToCallAfterCancellation, goal);
						return (null, result.Error);
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
				var goals = prParser.LoadAllGoals(true);
				
				return new StepError($"Instruction file could not be loaded for {step.RelativePrPath}", step, Key: "InstructionFileNotLoaded");
			}
			step.Instruction = instruction;
			return null;
		}

		private async Task KeepAlive()
		{
			var alives = appContext.GetOrDefault<List<Alive>>("KeepAlive");
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

					alives = appContext.GetOrDefault<List<Alive>>("KeepAlive");
					if (alives != null && alives.Count > 0)
					{
						var aliveTaskType = alives.FirstOrDefault(p => p.Key == "WaitForExecution");
						if (aliveTaskType?.Instances != null)
						{
							for (int i = 0; i < aliveTaskType.Instances.Count; i++)
							{
								var engineWait = (EngineWait)aliveTaskType.Instances[i];

								if (engineWait.task.IsFaulted)
								{
									Console.WriteLine(engineWait.task.Exception.Flatten().ToString());
								}

								if (engineWait.task.IsCompleted)
								{
									aliveTaskType.Instances.Remove(engineWait);
									// Engine is returned inside the async task (PseudoRuntime)
									i--;
								}
							}
							if (aliveTaskType.Instances.Count == 0)
							{
								alives.Remove(aliveTaskType);
							}
						}
					}
					// EnginePool cleanup is handled automatically by EnginePoolService timer
				}
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
								// Pooled engines will reload goals on next use when re-parsed
							}
						}, TaskScheduler.Default);
				}
			};
			fileWatcher.IncludeSubdirectories = true;
			fileWatcher.EnableRaisingEvents = true;
		}



		private Goal? GetStartGoal(string goalName)
		{
			string prPath = fileSystem.Path.Join(goalName.Replace(".goal", ""), ISettings.GoalFileName);
			string absolutePath = fileSystem.Path.Join(fileSystem.BuildPath, prPath);

			return prParser.GetGoal(absolutePath);
		}

		public Goal? GetGoal(string goalName, Goal? callingGoal)
		{
			var goal = prParser.GetGoalByAppAndGoalName(fileSystem.RootDirectory, goalName, callingGoal);
			if (goal != null) return goal;

			return prParser.GetGoalByAppAndGoalName(fileSystem.SystemDirectory, goalName, callingGoal);
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

		public void ReloadGoals()
		{
			prParser.ForceLoadAllGoals();
		}

		/// <summary>
		/// Error handling for step execution with retry support.
		/// Returns: (shouldRetry, error)
		/// </summary>
		private async Task<(bool ShouldRetry, IError? Error)> HandleStepErrorFlat(
			Goal goal, GoalStep step, int stepIndex, IError error, int retryCount, PLangContext context)
		{
			// Already handled or null - nothing to do
			if (error == null || error is IErrorHandled) return (false, error);

			// ThrowError module errors should propagate
			if (step.ModuleType == "PLang.Modules.ThrowErrorModule" && step.Instruction?.Function.Name == "Throw")
			{
				return (false, error);
			}

			// Handle AskUser errors - prompt and retry
			if (error is Errors.AskUser.AskUserError aue)
			{
				var (answer, answerError) = await AskUser.GetAnswer(this, context, aue.Message);
				if (answerError != null) return (false, answerError);

				var (_, callbackError) = await aue.InvokeCallback([answer]);
				if (callbackError != null) return (false, callbackError);

				return (true, null); // Retry the step
			}

			// Handle FileAccess errors - request permission and retry
			if (error is FileAccessRequestError fare)
			{
				var (isHandled, handlerError) = await HandleFileAccessError(fare, context);
				if (handlerError != null)
				{
					return (false, ErrorHelper.GetMultipleError(error, handlerError));
				}
				return (true, null); // Retry the step
			}

			// Check for step-defined error handlers with retry
			var errorHandler = StepHelper.GetErrorHandlerForStep(step.ErrorHandlers, error);
			if (errorHandler != null && HasRetriedToRetryLimit(errorHandler, retryCount))
			{
				return (false, error); // Hit retry limit
			}

			// Pre-event retry
			if (ShouldRunRetry(errorHandler, true))
			{
				logger.LogDebug($"Error - retry before event. Attempt {retryCount}/{errorHandler?.RetryHandler?.RetryCount}");
				if (errorHandler?.RetryHandler?.RetryDelayInMilliseconds > 0)
				{
					await Task.Delay(errorHandler.RetryHandler.RetryDelayInMilliseconds.Value);
				}
				return (true, null); // Retry the step
			}

			// Run step error events
			var stepErrorResult = await eventRuntime.RunOnErrorStepEvents(error, goal, step);
			if (stepErrorResult.Error != null && stepErrorResult.Error != error)
			{
				// Event handled the error differently
				if (stepErrorResult.Error is IErrorHandled)
				{
					return (false, null); // Error was handled
				}
				return (false, stepErrorResult.Error);
			}

			// Post-event retry (set by error event or handler)
			if (step.Retry || ShouldRunRetry(errorHandler, false))
			{
				step.Retry = false; // Reset
				logger.LogDebug($"Error - retry after event. Attempt {retryCount}/{errorHandler?.RetryHandler?.RetryCount}");
				if (errorHandler?.RetryHandler?.RetryDelayInMilliseconds > 0)
				{
					await Task.Delay(errorHandler.RetryHandler.RetryDelayInMilliseconds.Value);
				}
				return (true, null); // Retry the step
			}

			return (false, stepErrorResult.Error ?? error);
		}

		/// <summary>
		/// Core goal execution. Events are treated as steps and executed inline.
		/// </summary>
		private async Task<(object? Variables, IError? Error)> RunGoalInternal(Goal goal, PLangContext context, uint delayMs = 0, int startStepIndex = 0)
		{
			context.CallStack.EnterGoal(goal, context.Event);

			if (delayMs > 0) await Task.Delay((int)delayMs);

			logger.LogDebug($"[Start] Goal {goal.GoalName}");
			AppContext.SetSwitch("Runtime", true);
			SetLogLevel(goal.Comment);

			object? returnValues = null;
			int stepIndex = startStepIndex;

			try
			{
				// Skip before-goal events if starting from a specific step (resuming)
				if (startStepIndex == 0)
				{
					// Execute before-goal events as steps
					var beforeGoalEvents = await eventRuntime.GetBeforeGoalEvents(goal);
					foreach (var evt in beforeGoalEvents)
					{
						var evtResult = await eventRuntime.ExecuteEvent(evt, goal, null);
						if (evtResult.Error != null) return evtResult;
					}
				}

				// Handle callback continuation (takes precedence over startStepIndex)
				if (context.Callback?.CallbackInfo.GoalHash == goal.Hash)
				{
					stepIndex = context.Callback.CallbackInfo.StepIndex;
					if (stepIndex > goal.GoalSteps.Count - 1)
					{
						return (null, new Error("stepIndex is higher than steps in goal"));
					}
					goal.GoalSteps[stepIndex].Execute = true;
				}

				// Execute steps in a flat loop with events as steps
				for (; stepIndex < goal.GoalSteps.Count; stepIndex++)
				{
					var step = goal.GoalSteps[stepIndex];
					context.CallingStep = step;
					goal.CurrentStepIndex = stepIndex;
					step.Stopwatch = Stopwatch.StartNew();
					context.CallStack.SetCurrentStep(step, stepIndex);

					SetStepLogLevel(step);

					try
					{
						// Skip if already executed (RunOnce)
						if (HasExecuted(step)) continue;

						step.UniqueId = Guid.NewGuid().ToString();

						// Load instruction
						var loadError = LoadInstruction(goal, step);
						if (loadError != null) return (null, loadError);

						// Execute before-step events as steps (only once, even on retry)
						var beforeStepEvents = await eventRuntime.GetBeforeStepEvents(goal, step);
						foreach (var evt in beforeStepEvents)
						{
							var evtResult = await eventRuntime.ExecuteEvent(evt, goal, step);
							if (evtResult.Error != null) return (null, evtResult.Error);
						}

						// Execute the step with retry support
						int retryCount = 0;
						object? stepReturnValue = null;
						IError? stepError = null;

						while (true)
						{
							(stepReturnValue, stepError) = await ProcessPrFile(goal, step, stepIndex, context);

							if (stepError == null) break; // Success

							// Handle error - check if we should retry
							var (shouldRetry, handledError) = await HandleStepErrorFlat(goal, step, stepIndex, stepError, retryCount, context);

							if (!shouldRetry)
							{
								stepError = handledError;
								break; // No retry, exit loop
							}

							retryCount++;
							stepError = null; // Clear for retry
						}

						if (stepError != null)
						{
							if (stepError is MultipleError me && !me.IsErrorHandled)
							{
								return (stepReturnValue, stepError);
							}
							if (stepError is EndGoal endGoal)
							{
								if (!endGoal.EndingGoal?.RelativePrPath.Equals(goal.RelativePrPath) == true)
								{
									return (stepReturnValue, endGoal);
								}
								// EndGoal targets this goal, exit normally
								returnValues = stepReturnValue;
								break;
							}
							// Other unhandled errors
							if (stepError is not IErrorHandled)
							{
								return (stepReturnValue, stepError);
							}
						}
						else
						{
							returnValues = stepReturnValue;
						}

						// Execute after-step events as steps (only once, even after retry)
						var afterStepEvents = await eventRuntime.GetAfterStepEvents(goal, step);
						foreach (var evt in afterStepEvents)
						{
							var evtResult = await eventRuntime.ExecuteEvent(evt, goal, step);
							if (evtResult.Error != null) return evtResult;
						}
					}
					finally
					{
						ResetStepLogLevel(goal);
						step.Stopwatch?.Stop();
						context.CallStack.CompleteCurrentStep(returnValues, null);
					}
				}

				// Execute after-goal events as steps
				var afterGoalEvents = await eventRuntime.GetAfterGoalEvents(goal);
				foreach (var evt in afterGoalEvents)
				{
					var evtResult = await eventRuntime.ExecuteEvent(evt, goal, null);
					if (evtResult.Error != null) return evtResult;
				}

				return (returnValues, null);
			}
			catch (Exception ex)
			{
				var error = new Error(ex.Message, Exception: ex);
				var eventError = await HandleGoalError(error, goal, stepIndex, context);
				return eventError;
			}
			finally
			{
				AppContext.SetData("GoalLogLevelByUser", null);
				logger.LogDebug($"[End] Goal: {goal.GoalName} => {context.CallStack.CurrentFrame?.Duration}");
				context.CallStack.ExitGoal();
			}
		}
	}
}
