using AngleSharp.Dom;
using LightInject;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using NBitcoin.Secp256k1;
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
using PLang.Services.OutputStream.Sinks;
using PLang.Utils;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using static PLang.Modules.BaseBuilder;
using static PLang.Modules.MockModule.Program;
using static PLang.Runtime.PseudoRuntime;
using static PLang.Utils.StepHelper;

namespace PLang.Runtime
{
	public interface IEngine : IDisposable
	{
		string Id { get; init; }
		public string Name { get; set; }
		void SetParentEngine(IEngine engine);
		IEngine? ParentEngine { get; }
		/// <summary>
		/// The app's keep-alive registry. Every engine returns the root engine's instance.
		/// </summary>
		KeepAliveRegistry KeepAliveRegistry { get; }
		/// <summary>The step that rented this engine from the EnginePool; null while it is in the pool and on the root engine.</summary>
		GoalStep? RentedFrom { get; set; }
		/// <summary>
		/// The context of the goal this engine runs, or ran last, so another thread can read its call stack and memory stack.
		/// The Context property cannot: it reads the calling thread's context. Null after the engine is returned.
		/// </summary>
		PLangContext? RunningContext { get; }
		/// <summary>The managed thread id the engine's current step started on; async steps can continue on another thread.</summary>
		int? RunningThreadId { get; }
		/// <summary>
		/// The error that ended Run: an error from the start goal, setup or the start/end of app events that was not handled
		/// in the goal and went to the app error events. Set even when an app error event (e.g. the system one that writes it out)
		/// handled it, so the console can exit non-zero. Null when Run completed without such an error.
		/// </summary>
		IError? AppError { get; }
		string Path { get; }
		bool IsInPool { get; set; }
		public DateTime LastAccess { get; set; }

		public static readonly string DefaultEnvironment = "production";
		public string Environment { get; set; }
		IPLangFileSystem FileSystem { get; }
		PrParser PrParser { get; }
		ConcurrentDictionary<string, Engine.LiveConnection> LiveConnections { get; set; }

		IServiceContainer Container { get; }
		List<IEngine> ChildEngines { get; set; }
		IAppCache AppCache { get; }
		IOutputSink UserSink { get; set; }
		IOutputSink SystemSink { get; set; }
		ResolveEventHandler AsmHandler { get; set; }
		EnginePool EnginePool { get; set; }
		List<ISerializer> Serializers { get; set; }
		PLangContext Context { get; }

		void AddContext(string key, object value);
		PLangAppContext GetAppContext();
		void Init(IServiceContainer container);
		Task<(object? Variables, IError? Error)> Run(string goalToRun, PLangContext context);
		/// <summary>Runs the setup goals; what Run does at start unless the process is in standby.</summary>
		Task<(object? Variables, IError?)> RunSetupGoals(PLangContext context);
		Task<(object? Variables, IError? Error)> RunGoal(Goal goal, PLangContext context, uint waitForXMillisecondsBeforeRunningGoal = 0);
		Goal? GetGoal(string goalName, Goal? callingGoal = null);
		Task<(object? ReturnValue, IError? Error)> RunFromStep(string prFile, PLangContext context);
		Task<(object? ReturnValue, IError? Error)> ProcessPrFile(Goal goal, GoalStep goalStep, int stepIndex, PLangContext context);
		IEventRuntime GetEventRuntime();

		//EnginePool GetEnginePool(string rootPath);
		void Reset(bool reset = false);
		Task<(object? Variables, IError? Error)> RunGoal(GoalToCallInfo goalToCall, Goal parentGoal, PLangContext context, uint waitForXMillisecondsBeforeRunningGoal = 0);
		Task<IEngine> RentAsync(GoalStep callingStep);
		void Return(IEngine engine, bool reset = false);
		void ReloadGoals();
		T GetProgram<T>() where T : BaseProgram;
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
		public IOutputSink SystemSink { get; set; }
		public IOutputSink UserSink { get; set; }
		public List<ISerializer> Serializers { get; set; }
		private PrParser prParser;
		private PLangAppContext appContext;
		public PrParser PrParser { get { return prParser; } }
		public DateTime LastAccess { get; set; }

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

		IEngine? _parentEngine = null;
		public IEngine? ParentEngine { get { return _parentEngine; } }

		private readonly KeepAliveRegistry keepAliveRegistry = new();
		public KeepAliveRegistry KeepAliveRegistry
		{
			get
			{
				IEngine engine = this;
				while (engine.ParentEngine != null) engine = engine.ParentEngine;
				return (engine == this) ? keepAliveRegistry : engine.KeepAliveRegistry;
			}
		}

		public GoalStep? RentedFrom { get; set; }
		public PLangContext? RunningContext { get; private set; }
		public int? RunningThreadId { get; private set; }
		public IError? AppError { get; private set; }

		public IPLangFileSystem FileSystem { get { return fileSystem; } }
		public ResolveEventHandler AsmHandler { get; set; }


		ConcurrentQueue<IEngine> pool = new();
		public bool IsInPool { get; set; }
		public ConcurrentQueue<IEngine> Pool { get { return pool; } }
		public EnginePool EnginePool { get; set; }

		// Process wide, so it is added once for all engines: the pool creates and disposes engines all the time,
		// and a handler per engine would print every unhandled exception once per engine ever created.
		static Engine()
		{
			AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
			{
				Console.WriteLine($"Unhandled exception: {args.ExceptionObject}");
			};
		}

		public Engine()
		{
			Id = Guid.NewGuid().ToString();
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

			EnginePool = new EnginePool(container.GetInstance<IEngine>());

			this.prParser = container.GetInstance<PrParser>();
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




		public void SetParentEngine(IEngine parentEngine)
		{
			this._parentEngine = parentEngine;
			if (fileSystem.RootDirectory != parentEngine.Path)
			{
				fileSystem.AddFileAccess(new FileAccessControl(fileSystem.RootDirectory, parentEngine.Path, ProcessId: this.fileSystem.Id));
			}

		}

		public async Task<IEngine> RentAsync(GoalStep callingStep)
		{
			var engine = await EnginePool.RentAsync(callingStep);
			try
			{
				// Settings keys are prefixed with the engine's Environment (Settings.GetKey), and every
				// rented engine has its own container and Settings. Without this, an app that runs
				// `set environment "dev"` reads "production.X" in every request engine and every
				// "dont wait" goal, and finds no setting.
				engine.Environment = this.Environment;
				// Each rented engine has its own file system, so the grants this engine holds for its process
				// (e.g. `give access to "/proc"`) are given to the rented one for as long as it is rented;
				// Reset clears them when it goes back to the pool.
				foreach (var fileAccess in fileSystem.GetProcessFileAccess())
				{
					engine.FileSystem.AddFileAccess(fileAccess with { ProcessId = engine.FileSystem.Id });
				}
			}
			catch
			{
				// The caller never gets the engine, so it cannot return it.
				EnginePool.Return(engine);
				throw;
			}
			return engine;
		}
		public void Return(IEngine engine, bool reset = false)
		{
			EnginePool.Return(engine, reset);
			
		}

		public void Reset(bool reset = false)
		{
			if (ParentEngine == null)
			{
				throw new Exception($"Parent engine is null on return. {ErrorReporting.CreateIssueShouldNotHappen}");
			}
			LastAccess = DateTime.Now;
			appContext = ParentEngine.GetAppContext();
			if (fileSystem == null)
			{
				Console.WriteLine($"???????????? - fileSystem is null???????????? - {Name}");
			}
			else
			{
				fileSystem.ClearFileAccess();
			}

			/*
			foreach (var listofDisp in listOfDisposables)
			{
				foreach (var disposable in listofDisp.Value)
				{
					disposable.Dispose();
				}
			}
			listOfDisposables.Clear();*/
			
			contextAccessor.Current = null;
			var msa = container.GetInstance<IMemoryStackAccessor>();
			msa.Current = null;
			RentedFrom = null;
			RunningContext = null;
			RunningThreadId = null;
			

		
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
					(returnVars, error) = await RunAppErrorEvents(new Error($"Goal '{goalToRun}' not found"));
					if (returnVars != null) vars.Add(returnVars);
					if (error != null) return (ov, error);
				}

				(returnVars, error) = await eventRuntime.RunStartEndEvents(EventType.Before, EventScope.StartOfApp, goal);
				if (returnVars != null) vars.Add(returnVars);
				if (error != null)
				{
					(returnVars, error) = await RunAppErrorEvents(error);
					if (returnVars != null) vars.Add(returnVars);
					if (error != null) return (ov, error);
				}

				// A standby process does not run the setup goals at start, `run setup` runs them on demand
				(returnVars, error) = RuntimeMode.Standby ? (null, null) : await RunSetup(goal, context);
				if (returnVars != null) vars.Add(returnVars);
				if (error != null)
				{
					(returnVars, error) = await RunAppErrorEvents(error);
					if (returnVars != null) vars.Add(returnVars);
					if (error != null) return (ov, error);
				}
				if (goalToRun.RemoveExtension().Equals("setup", StringComparison.OrdinalIgnoreCase)) return (ov, null);


				(returnVars, error) = await RunGoal(goal, context);
				if (returnVars != null) vars.Add(returnVars);
				// Start ending early (`end goal`, a question to a peer that it goes on from later) is not an error of the app.
				if (error is EndGoal) error = null;
				if (error != null)
				{
					(returnVars, error) = await RunAppErrorEvents(error);
					if (returnVars != null) vars.Add(returnVars);
					if (error != null) return (ov, error);
				}


				(returnVars, error) = await eventRuntime.RunStartEndEvents(EventType.After, EventScope.StartOfApp, goal);
				if (returnVars != null) vars.Add(returnVars);
				if (error != null)
				{
					(returnVars, error) = await RunAppErrorEvents(error);
					if (returnVars != null) vars.Add(returnVars);
					if (error != null) return (ov, error);
				}


				WatchForRebuild();

			}
			catch (Exception ex)
			{
				logger.LogError(ex, "OnStart");
				error = new Error(ex.Message, Exception: ex);
				AppError ??= error;
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

		// The app error events decide what is done with an error that ends the app (the system event writes it out and
		// returns no error). AppError keeps the first one, so the process can still report that the app ended on an error.
		private Task<(object? Variables, IError? Error)> RunAppErrorEvents(IError appError)
		{
			if (appError is not IErrorHandled) AppError ??= appError;
			return eventRuntime.AppErrorEvents(appError);
		}


		private static CancellationTokenSource debounceTokenSource;
		private static readonly object debounceLock = new object();
		private IFileSystemWatcher? fileWatcher = null;
		private IFileSystemWatcher? buildFolderWatcher = null;
		private readonly object watcherLock = new object();

		public virtual void Dispose()
		{

			if (this.disposed)
			{
				return;
			}
			buildFolderWatcher?.Dispose();
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


		public async Task<(object? Variables, IError?)> RunSetupGoals(PLangContext context)
		{
			return await RunSetup(null, context);
		}

		private async Task<(object? Variables, IError?)> RunSetup(Goal? startGoal, PLangContext context)
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
			RunningContext = context;
			try
			{
				context.CallStack.EnterGoal(goal, context.Event);
			}
			catch (CallStackOverflowException ex)
			{
				// Return as a normal error instead of letting it escape. It used to be thrown before
				// the try/finally below, so it bypassed the goal error handling entirely and surfaced
				// as an opaque "UnhandledError: One or more errors occurred. (1000 frames)".
				// No frame was pushed, so there is nothing to unwind here.
				logger.LogError(ex, "Call stack overflow entering goal {0}", goal.GoalName);
				return (null, new GoalError(ex.Message, goal, "CallStackOverflow", 500, ex,
					FixSuggestion: "A goal is calling itself (or a cycle of goals) with no terminating condition. Check the condition that guards the recursive 'call goal' step."));
			}

			// Deep goal nesting otherwise grows the *native* stack. RunGoal is async, but when every
			// await inside completes synchronously the state machine never suspends, so nested goals
			// pile up real stack frames and the process dies with a StackOverflowException (~460 goals
			// deep) - which .NET cannot catch, taking the whole runtime down with it. Forcing a yield
			// every so often moves the continuation to the thread pool and resets the stack, so
			// MaxDepth stays a logical limit instead of a race against the physical stack.
			if (context.CallStack.Depth % 32 == 0) await Task.Yield();

			if (waitForXMillisecondsBeforeRunningGoal > 0) await Task.Delay((int)waitForXMillisecondsBeforeRunningGoal);
			
			logger.LogDebug($"[Start] Goal {goal.GoalName}");

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

				logger.LogDebug($" - Running Before event on goal - {context.CallStack.CurrentFrame.Duration}");

				var result = await eventRuntime.RunGoalEvents(EventType.Before, goal);
				// `end goal, and previous` in a before event ends this goal; the goal that called it goes on.
				if (EndsThisGoal(result.Error, goal)) return (result.Variables, null);
				if (result.Error != null) return result;

				logger.LogDebug($" - Event done, now running Steps - {context.CallStack.CurrentFrame.Duration}");

				//if (await CachedGoal(goal)) return null;
				(var returnValues, stepIndex, var stepError) = await RunSteps(goal, context, 0);

				if (stepError != null && stepError is not IErrorHandled) return (returnValues, stepError);
				//await CacheGoal(goal);
				logger.LogDebug($" - Steps done, running After events - {context.CallStack.CurrentFrame.Duration}");

				result = await eventRuntime.RunGoalEvents(EventType.After, goal);
				if (EndsThisGoal(result.Error, goal)) return (returnValues, stepError);
				if (result.Error != null) return result;

				logger.LogTrace($" - After events done - {context.CallStack.CurrentFrame.Duration}");

				return (returnValues, stepError);

			}
			catch (Exception ex)
			{
				var error = new Error(ex.Message, Exception: ex);
				//if (context.ContainsKey(ReservedKeywords.IsEvent)) return error;

				var eventError = await HandleGoalError(error, goal, stepIndex, context);
				return eventError;
			}
			finally
			{
				// AppContext.SetData("GoalLogLevelByUser", null) used to run here after every goal. The key is
				// never read anywhere (AppContext.SetData takes a process wide lock), so the write is gone.
				logger.LogDebug($"[End] Goal: {goal.GoalName} => {context.CallStack.CurrentFrame.Duration}");
				context.CallStack.ExitGoal();


			}

		}

		private async Task DisposeGoal(Goal goal)
		{

		}

		// A callback resumes the goal at the ask step. When that step sits inside an if block, the
		// if itself does not run again, so the steps it had enabled after the ask stay disabled and
		// the goal continues at the top level with whatever those steps were meant to load.
		// The condition was true in the original request (we reached the ask), so enable the
		// steps that follow on the same level, follow the level out as blocks close, and leave
		// deeper blocks to their own if. Top-level steps run on their own.
		private void EnableStepsAfterCallback(Goal goal, int stepIndex, PLangContext context)
		{
			int indent = goal.GoalSteps[stepIndex].Indent;
			if (indent == 0) return;

			for (int i = stepIndex + 1; i < goal.GoalSteps.Count; i++)
			{
				var step = goal.GoalSteps[i];
				if (step.Indent == 0) return;
				if (step.Indent < indent) indent = step.Indent;
				if (step.Indent == indent)
				{
					step.Execute = true;
					context.CallStack.CurrentFrame.SetStepEnabled(i, true);
				}
			}
		}

		private async Task<(object? ReturnValue, int StepIndex, IError? Error)> RunSteps(Goal goal, PLangContext context, int stepIndex = 0)
		{
			// Timestamps instead of Stopwatch objects: the elapsed time is only read by log lines that are
			// not built when the level is off, so a goal (and each of its steps) no longer allocates stopwatches.
			long goalStart = Stopwatch.GetTimestamp();
			object? returnValues = null;
			IError? error = null;
			logger.LogDebug($"  - Goal {goal.GoalName} starts - {ElapsedMs(goalStart)}");

			if (context.Callback?.CallbackInfo.GoalHash == goal.Hash)
			{
				stepIndex = context.Callback.CallbackInfo.StepIndex;
				if (stepIndex > goal.GoalSteps.Count - 1)
				{
					return (null, stepIndex, new Error("stepIndex is higher than steps in goal"));
				}
				goal.GoalSteps[stepIndex].Execute = true;
				context.CallStack.CurrentFrame.SetStepEnabled(stepIndex, true);
				EnableStepsAfterCallback(goal, stepIndex, context);
			}

			for (; stepIndex < goal.GoalSteps.Count; stepIndex++)
			{

				long stepStart = Stopwatch.GetTimestamp();
				logger.LogTrace($"   - Step idx {stepIndex} starts - {ElapsedMs(stepStart)}");


				logger.LogDebug($"   - [S] RunStep:{goal.GoalSteps[stepIndex].PrFileName} - {ElapsedMs(stepStart)}");
				(returnValues, error) = await RunStep(goal, stepIndex, context);
				logger.LogTrace($"   - [E] RunStep:{goal.GoalSteps[stepIndex].PrFileName} - {ElapsedMs(stepStart)}");
				
				
				if (error != null)
				{
					logger.LogTrace($"   - Step idx {stepIndex} has ERROR - {ElapsedMs(stepStart)}");
					if (error is MultipleError me && me.IsErrorHandled)
					{
						var hasEndGoal = FindErrorHandled(me);
						if (hasEndGoal != null) error = hasEndGoal;
					}
					if (error is EndGoal endGoal)
					{
						logger.LogTrace($"   - Exiting goal because of end goal: {endGoal.Goal?.RelativeGoalPath} - {ElapsedMs(stepStart)}");
						try
						{
							if (!endGoal.EndingGoal?.RelativePrPath.Equals(goal.RelativePrPath) == true)
							{
								logger.LogTrace($"   - End goal doing return: {endGoal.Goal?.RelativeGoalPath} - {ElapsedMs(stepStart)}");
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

						logger.LogTrace($"   - End goal doing continue: {endGoal.Goal?.RelativeGoalPath} - {ElapsedMs(stepStart)}");
						return (returnValues, stepIndex, null);
					}
					var errorInGoalErrorHandler = await HandleGoalError(error, goal, stepIndex, context);
					if (errorInGoalErrorHandler.Error != null) return (returnValues, stepIndex, errorInGoalErrorHandler.Error);
					if (errorInGoalErrorHandler.Error is IErrorHandled) error = null;
				}
				logger.LogDebug($"   - Step idx {stepIndex} done - {ElapsedMs(stepStart)} - Current for all steps: {ElapsedMs(goalStart)}");
			}
			logger.LogTrace($"  - All steps done in {goal.GoalName} - {ElapsedMs(goalStart)}");
			return (returnValues, stepIndex, error);
		}

		private static long ElapsedMs(long startTimestamp) => (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;




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

			var result = await RunSteps(goal, context, step.Index);
			return (result.ReturnValue, result.Error);
		}

		private async Task<(object? ReturnValue, IError? Error)> RunStep(Goal goal, int goalStepIndex, PLangContext context, int retryCount = 0)
		{
			(object? ReturnValues, IError? Error)? result = null;

			var step = goal.GoalSteps[goalStepIndex];
			context.CallingStep = step;

			GoalRuntimeState.SetCurrentStepIndex(goal, goalStepIndex);
			// step is shared by every request, so this run times itself with its own timestamp (no stopwatch
			// object per step). step.Stopwatch is only read by the debug output, so it is only set in debug mode
			// (ProcessPrFile creates one if a log line needs it and it is missing).
			long stepStartTimestamp = Stopwatch.GetTimestamp();
			if (context.DebugMode) step.Stopwatch = Stopwatch.StartNew();

			context.CallStack.SetCurrentStep(step, goalStepIndex);
			RunningContext = context;
			RunningThreadId = System.Environment.CurrentManagedThreadId;

			SetStepLogLevel(step);
			try
			{
				
				
				if (HasExecuted(step, context)) return (null, null);

				// step.UniqueId = Guid.NewGuid() was written here for every step execution, on the shared step.
				// Nothing reads it (not the runtime, not the goals of rafbokin, os or the tests), so the GUID is gone.
				logger.LogTrace($"     - Load instruction for step: {step.Text.MaxLength(20)} - {ElapsedMs(stepStartTimestamp)}");

				// The instruction is cached on the step; it is only (re)loaded when the step has none.
				if (step.Instruction == null)
				{
					var error = LoadInstruction(goal, step);
					if (error != null)
					{
						return (null, error);
					}
				}
				logger.LogTrace($"     - Have instruction - {ElapsedMs(stepStartTimestamp)}");
				if (retryCount == 0)
				{
					logger.LogTrace($"     - Before event starts - {ElapsedMs(stepStartTimestamp)}");
					// Only run event one time, even if step is tried multiple times
					var stepEventResult = await eventRuntime.RunStepEvents(EventType.Before, goal, step);
					if (stepEventResult.Error != null) return (null, stepEventResult.Error);
				}

				logger.LogTrace($"     - ProcessPrFile {step.PrFileName} - {ElapsedMs(stepStartTimestamp)}");

				result = await ProcessPrFile(goal, step, goalStepIndex, context);

				if (result.Value.Error != null)
				{
					result = await HandleStepError(goal, step, goalStepIndex, result.Value.Error, retryCount, context);
					if (result.Value.Error != null && result.Value.Error is MultipleError me && !me.IsErrorHandled) return result.Value;
				}
				logger.LogTrace($"     - Done with ProcessPrFile, doing after events - {ElapsedMs(stepStartTimestamp)}");

				if (retryCount == 0)
				{
					// Only run event one time, even if step is tried multiple times, retryCount is 0 event after retry when callstack is traversed back up
					var stepEventResult = await eventRuntime.RunStepEvents(EventType.After, goal, step);
					if (stepEventResult.Error != null) return stepEventResult;
				}

				logger.LogTrace($"     - Done with after events - {ElapsedMs(stepStartTimestamp)}");
				return result.Value;
			}
			catch (Exception stepException)
			{

				var error = new ExceptionError(stepException, stepException.Message, goal, step);
				result = await HandleStepError(goal, step, goalStepIndex, error, retryCount, context);

				return result.Value;
			}
			finally
			{
				logger.LogTrace($"     - Reset log level - {ElapsedMs(stepStartTimestamp)}");
				ResetStepLogLevel(goal);
				logger.LogDebug($"     - Step all done - {ElapsedMs(stepStartTimestamp)}");

				object? returnValues = null;
				IError? error = null;
				if (result != null)
				{
					returnValues = result.Value.ReturnValues;
					error = result.Value.Error;
				}
				context.CallStack.CompleteCurrentStep(returnValues, error);
			}

		}



		private async Task<(object? ReturnValue, IError? Error)> HandleStepError(Goal goal, GoalStep step, int goalStepIndex, IError? error, int retryCount, PLangContext context)
		{
			if (error == null || error is IErrorHandled) return (null, error);
			if (step.ModuleType == "PLang.Modules.ThrowErrorModule" && step.Instruction?.Function.Name == "Throw") return (null, error);

			if (error is MissingSettingsException missingSetting)
			{
				var settingError = await MissingSettingsHelper.HandleMissingSetting(this, context, missingSetting);
				if (settingError != null) return (null, settingError);
				return await RunStep(goal, goalStepIndex, context);
			}
			if (error is Errors.AskUser.AskUserError aue)
			{
				var (answer, answerError) = await AskUser.GetAnswer(this, context, aue.Message);
				if (answerError != null) return (null, answerError);

				var (_, callbackError) = await aue.InvokeCallback([answer]);
				if (callbackError != null) return (null, callbackError);

				return await RunStep(goal, goalStepIndex, context);
			}

			if (error is FileAccessRequestError fare)
			{
				(var isHandled, var handlerError) = await HandleFileAccessError(fare, context);
				if (handlerError != null)
				{
					return (null, ErrorHelper.GetMultipleError(error, handlerError));
				}

				return await RunStep(goal, goalStepIndex, context);

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
					return await RunStep(goal, goalStepIndex, context, ++retryCount);
				}
			}

			var eventRuntime = container.GetInstance<IEventRuntime>();
			var stepErrorResult = await eventRuntime.RunOnErrorStepEvents(error, goal, step);
			if (stepErrorResult.Error != null)
			{
				return stepErrorResult;
			}

			// retry can be set by a goal in RunOnErrorStepEvents (retry step), kept per request
			// since step is shared by every request; TakeRetry also resets it
			bool retryRequested = GoalRuntimeState.TakeRetry(step);
			if (retryRequested || ShouldRunRetry(errorHandler, false))
			{

				logger.LogDebug($"Error occurred - After goal to call - Will retry in {errorHandler?.RetryHandler?.RetryDelayInMilliseconds}ms. Attempt nr. {retryCount} of {errorHandler?.RetryHandler?.RetryCount}\nError:{error}");
				if (errorHandler?.RetryHandler?.RetryDelayInMilliseconds != null && errorHandler.RetryHandler.RetryDelayInMilliseconds > 0)
				{
					await Task.Delay(errorHandler.RetryHandler.RetryDelayInMilliseconds.Value);
				}
				return await RunStep(goal, goalStepIndex, context, ++retryCount);
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

		private bool ShouldExecute(GoalStep step, PLangContext context)
		{
			if (step.Indent > 0) return context.CallStack.CurrentFrame.IsStepEnabled(step.Index);
			return step.Execute;
		}

		private bool HasExecuted(GoalStep step, PLangContext context)
		{
			if (!ShouldExecute(step, context)) return true;
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
			if (stepIndex < goal.GoalSteps.Count && !ShouldExecute(goal.GoalSteps[stepIndex], context))
			{
				logger.LogDebug($"Step is disabled: {goal.GoalSteps[stepIndex].Text}");
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

			// A CancellationTokenSource with CancelAfter(30 s) used to be created here for every step (a timer
			// per step). Its token was never passed to anything, so it could not cancel the step; it only
			// cost a timer allocation and registration. Removed without changing behaviour.
			{
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
						context.CallStack.CurrentFrame.SetStepEnabled(step.Index, false);
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
			// Rented engines are in the registry too but do not keep the app running: they serve a request or a task
			// that a listener or a "dont wait" entry keeps alive, and are returned when it ends.
			var registry = KeepAliveRegistry;
			if (registry.KeepsAppAliveCount == 0) return;

			// The app has started and stays up to serve (webserver, scheduler): fill the engine pool in the background.
			if (ParentEngine == null) EnginePool.StartWarmUp();
			logger.LogWarning("Keeping app alive, reasons:");
			foreach (var kind in registry.GetKeepAppAliveSnapshot().Select(p => p.Kind).Distinct())
			{
				logger.LogWarning(" - " + kind);
			}

			while (registry.KeepsAppAliveCount > 0)
			{
				await Task.Delay(1000);

				// A finished "dont wait" task has already returned the engine it rented (PseudoRuntime.RunGoal).
				foreach (var entry in registry.RemoveCompletedTasks())
				{
					var task = ((PlangTask)entry.Instance).Task;
					if (task.IsFaulted && task.Exception != null)
					{
						Console.WriteLine(task.Exception.Flatten().ToString());
					}
				}

				EnginePool.CleanupEngines();
			}
		}



		// Reloads the goals when the .build folder changes. A deploy can write .pr files in place (git pull, cp), move them into place by
		// rename (rsync, scp, tar), add or remove goal folders, or replace the whole .build folder. The first watcher covers everything inside
		// .build; the second watches the folder holding .build, so a new .build folder gets a new watcher.
		private void WatchForRebuild()
		{
			if (ParentEngine != null) return;

			if (buildFolderWatcher == null)
			{
				string buildPath = fileSystem.BuildPath.TrimEnd(fileSystem.Path.DirectorySeparatorChar);
				string? parentPath = fileSystem.Path.GetDirectoryName(buildPath);
				if (parentPath != null && fileSystem.Directory.Exists(parentPath))
				{
					buildFolderWatcher = fileSystem.FileSystemWatcher.New(parentPath, fileSystem.Path.GetFileName(buildPath));
					buildFolderWatcher.NotifyFilter = NotifyFilters.DirectoryName;
					buildFolderWatcher.Created += OnBuildFolderReplaced;
					buildFolderWatcher.Renamed += OnBuildFolderReplaced;
					buildFolderWatcher.EnableRaisingEvents = true;
				}
			}

			WatchBuildFolder();
		}

		private void WatchBuildFolder()
		{
			lock (watcherLock)
			{
				fileWatcher?.Dispose();
				fileWatcher = null;

				if (!fileSystem.Directory.Exists(fileSystem.BuildPath)) return;

				var watcher = fileSystem.FileSystemWatcher.New(fileSystem.BuildPath);
				watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite;
				watcher.IncludeSubdirectories = true;
				watcher.Changed += OnBuildChanged;
				watcher.Created += OnBuildChanged;
				watcher.Deleted += OnBuildChanged;
				watcher.Renamed += OnBuildChanged;
				watcher.Error += (sender, e) =>
				{
					// The watcher stops after an error (the folder was removed, the event buffer overflowed). Start a new one and reload,
					// since changes may have been missed.
					if (!ReferenceEquals(sender, fileWatcher)) return;
					logger.LogWarning(e.GetException(), "Watcher on {BuildPath} failed, starting a new one", fileSystem.BuildPath);
					WatchBuildFolder();
					ScheduleReload();
				};
				watcher.EnableRaisingEvents = true;
				fileWatcher = watcher;
			}
		}

		private void OnBuildFolderReplaced(object sender, FileSystemEventArgs e)
		{
			WatchBuildFolder();
			ScheduleReload();
		}

		private void OnBuildChanged(object sender, FileSystemEventArgs e)
		{
			if (IsGoalChange(e)) ScheduleReload();
		}

		// A .pr file, or a goal folder created, moved or deleted. A folder's own Changed event (its timestamp) is not a change to the goals.
		private bool IsGoalChange(FileSystemEventArgs e)
		{
			if (IsPrFile(e.FullPath)) return true;
			if (e is RenamedEventArgs renamed && IsPrFile(renamed.OldFullPath)) return true;
			return e.ChangeType != WatcherChangeTypes.Changed && string.IsNullOrEmpty(fileSystem.Path.GetExtension(e.FullPath));
		}

		private bool IsPrFile(string? path)
		{
			return path != null && fileSystem.Path.GetExtension(path).Equals(".pr", StringComparison.OrdinalIgnoreCase);
		}

		// Waits until the build folder has been quiet for 1 second, so a deploy of many files reloads once.
		private void ScheduleReload()
		{
			lock (debounceLock)
			{
				debounceTokenSource?.Cancel();
				debounceTokenSource?.Dispose();
				debounceTokenSource = new CancellationTokenSource();

				Task.Delay(1 * 1000, debounceTokenSource.Token)
					.ContinueWith(t =>
					{
						if (t.IsCanceled) return;
						try
						{
							Console.Write(".");
							prParser.ForceLoadAllGoals();
							var error = eventRuntime.Reload();
							if (error != null)
							{
								Console.WriteLine(error);
							}

							foreach (var item in EnginePool.Pool)
							{
								// Rented engines share this parser, reloaded once above.
								if (item.PrParser != prParser) item.ReloadGoals();
								item.GetEventRuntime().Reload();
							}
						}
						catch (Exception ex)
						{
							logger.LogError(ex, "Reloading goals from {BuildPath}", fileSystem.BuildPath);
						}
					}, TaskScheduler.Default);
			}
		}



		private Goal? GetStartGoal(string goalName)
		{
			string name = goalName.EndsWith(".goal", StringComparison.OrdinalIgnoreCase) ? goalName[..^".goal".Length] : goalName;
			string absolutePath = fileSystem.Path.Join(fileSystem.BuildPath, name, ISettings.GoalFileName);
			if (fileSystem.File.Exists(absolutePath)) return prParser.GetGoal(absolutePath);

			// Goal names are case-insensitive, `plang setup` runs Setup.goal, on a case-sensitive file system too.
			// A goal in a folder of its own name, setup/Setup.goal, is found by the folder name.
			var goalFiles = fileSystem.Directory.Exists(fileSystem.BuildPath)
				? fileSystem.Directory.GetFiles(fileSystem.BuildPath, ISettings.GoalFileName, SearchOption.AllDirectories)
				: [];
			string lastSegment = fileSystem.Path.GetFileName(name.TrimEnd('/', '\\'));
			foreach (var candidate in new[] { name, fileSystem.Path.Join(name, lastSegment) })
			{
				var wanted = fileSystem.Path.GetFullPath(fileSystem.Path.Join(fileSystem.BuildPath, candidate, ISettings.GoalFileName));
				var match = goalFiles.FirstOrDefault(p => string.Equals(fileSystem.Path.GetFullPath(p), wanted, StringComparison.OrdinalIgnoreCase));
				if (match != null) return prParser.GetGoal(match);
			}
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


		private static bool EndsThisGoal(IError? error, Goal goal)
		{
			return error is EndGoal endGoal && !endGoal.Terminate
				&& endGoal.EndingGoal?.RelativePrPath.Equals(goal.RelativePrPath) == true;
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
	}
}
