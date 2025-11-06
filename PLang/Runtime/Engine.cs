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
using ReverseMarkdown.Converters;
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

		void AddContext(string key, object value);
		PLangAppContext GetAppContext();
		void Init(IServiceContainer container);
		Task<(object? Variables, IError? Error)> Run(string goalToRun, PLangContext context);
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
		public IOutputSink SystemSink { get; set; }
		public IOutputSink UserSink { get; set; }

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

		public IPLangFileSystem FileSystem { get { return fileSystem; } }
		public ResolveEventHandler AsmHandler { get; set; }


		ConcurrentQueue<IEngine> pool = new();
		public bool IsInPool { get; set; }
		public ConcurrentQueue<IEngine> Pool { get { return pool; } }
		public EnginePool EnginePool { get; set; }
		public Engine()
		{
			Id = Guid.NewGuid().ToString();

			AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
			{
				Console.WriteLine($"Unhandled exception: {args.ExceptionObject}");
			};
			LastAccess = DateTime.Now;


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

			var activeEvents = parentEngine.GetEventRuntime().GetActiveEvents();
			this.eventRuntime.SetActiveEvents(activeEvents);
		}

		/*
		(IEngine, ConcurrentQueue<IEngine>) GetPoolEngine(IEngine engine)
		{
			var pool = this.pool;
			var parentEngine = this._parentEngine;
			IEngine poolEngine = this;
			while (parentEngine != null)
			{
				poolEngine = parentEngine;
				pool = parentEngine.Pool;
				parentEngine = parentEngine.ParentEngine;
			}

			return (poolEngine, pool);
		}
		*/
		public async Task<IEngine> RentAsync(GoalStep callingStep)
		{
			return await EnginePool.RentAsync(callingStep);
			/*
			var (poolEngine, pool) = GetPoolEngine(this);

			Console.WriteLine($"RentAsync called - pool size BEFORE: {pool.Count} - Engine:{poolEngine.Name}({poolEngine.Id}) - {callingStep.Text.ReplaceLineEndings(" ").MaxLength(35)}");

			if (pool.TryDequeue(out var engine))
			{
				Console.WriteLine($"Reusing engine from pool({pool.Count}) - Engine:{poolEngine.Name}({poolEngine.Id}) -> {engine.Name}({engine.Id})");

				InitPerRequest(container, engine);
				return engine;
			}

			Console.WriteLine($"Pool was empty, creating new engine");
			engine = CreateEngine(this.Path);

			Process currentProcess = Process.GetCurrentProcess();
			long privateMemory = currentProcess.PrivateMemorySize64;
			Console.WriteLine($"After Create engine - Engine:{poolEngine.Name}({poolEngine.Id}) -> {engine.Name}({engine.Id}) - Private Memory: {privateMemory / 1024 / 1024} MB");

			return engine;
			*/
		}
		public void Return(IEngine engine, bool reset = false)
		{
			EnginePool.Return(engine, reset);
			/*
			var (poolEngine, pool) = GetPoolEngine(this);
			Console.WriteLine($"Return called - pool size BEFORE: {pool.Count} - Engine:{poolEngine.Name}({poolEngine.Id}) -> {engine.Name}({engine.Id})");

			engine.Reset(true);
			if (engine.FileSystem != null)
			{
				pool.Enqueue(engine);
				Console.WriteLine($"Returned - pool size AFTER: {pool.Count} - Engine:{poolEngine.Name}({poolEngine.Id}) -> {engine.Name}({engine.Id})");
			} else
			{
				Console.WriteLine($"File system null not returning: {pool.Count} - Engine:{poolEngine.Name}({poolEngine.Id}) -> {engine.Name}({engine.Id})");
			}*/



			/*
			var enginePool = GetEnginePool(Path);
			enginePool.Return(engine, reset);
			*/
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
			this.eventRuntime.GetActiveEvents().Clear();
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

			if (reset)
			{
				var prParser = container.GetInstance<PrParser>();
				prParser.ClearVariables();
			}
		}

		/*
		public static void InitPerRequest(IServiceContainer container, IEngine? engine = null)
		{
			engine ??= container.GetInstance<IEngine>();

			var msa = container.GetInstance<IMemoryStackAccessor>();
			var memoryStack = MemoryStack.New(container, engine);
			msa.Current = memoryStack;

			var context = new PLangContext(memoryStack, engine, ExecutionMode.Console);
			var ca = container.GetInstance<IPLangContextAccessor>();
			ca.Current = context;

		}

		private IEngine CreateEngine(string rootPath)
		{
			var serviceContainer = new ServiceContainer();

			serviceContainer.RegisterForPLang(rootPath, "/",
								container.GetInstance<IErrorHandlerFactory>(), container.GetInstance<IErrorSystemHandlerFactory>(), this);


			var engine = serviceContainer.GetInstance<IEngine>();
			engine.Name = $"Child - {Name}";

			InitPerRequest(serviceContainer);

			engine.Init(serviceContainer);
			engine.SetParentEngine(this);

			engine.SystemSink = this.SystemSink;
			engine.UserSink = this.UserSink;

			return engine;
		}*/
		/*
		public EnginePool GetEnginePool(string rootPath)
		{
			rootPath = rootPath.TrimEnd(fileSystem.Path.DirectorySeparatorChar);
			if (enginePools.TryGetValue(rootPath, out var pool))
			{
				Console.WriteLine($"found enginpool for {rootPath} - Name:'{Name}' - {contextAccessor.Current?.HttpContext?.Request.Path}");
				return pool;
			}

			var tempContext = container.GetInstance<PLangAppContext>();
			Process currentProcess = Process.GetCurrentProcess();
			long privateMemory = currentProcess.PrivateMemorySize64;
			Console.WriteLine($"Before Private Memory: {privateMemory / 1024 / 1024} MB");
			pool = new EnginePool(2, () =>
			{
				var engine = CreateEngine(rootPath);

				long privateMemory = currentProcess.PrivateMemorySize64;
				Console.WriteLine($"After Private Memory: {privateMemory / 1024 / 1024} MB");
				return engine;
			});

			enginePools.TryAdd(rootPath, pool);
			Console.WriteLine($"added {rootPath} has: {enginePools.Count} - Name:'{Name}' - {contextAccessor.Current?.HttpContext?.Request.Path} - {privateMemory / 1024 / 1024} MB");

			return pool;

		}
		*/
		public IEventRuntime GetEventRuntime()
		{
			return this.eventRuntime;
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
			this.prParser.ClearVariables();
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

				var result = await RunGoal(goal, context);
				if (result.Variables != null)
				{
					vars.Add(result.Variables);
				}
				if (result.Error != null) return (ov, result.Error);
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
			context.CallStack.EnterGoal(goal);

			if (waitForXMillisecondsBeforeRunningGoal > 0) await Task.Delay((int)waitForXMillisecondsBeforeRunningGoal);
			goal.Stopwatch = Stopwatch.StartNew();
			goal.UniqueId = Guid.NewGuid().ToString();

			logger.LogDebug($"[Start] Goal {goal.GoalName}");

			AppContext.SetSwitch("Runtime", true);
			SetLogLevel(goal.Comment);

			int stepIndex = -1;
			try
			{
				context.CallStack.SetPhase(ExecutionPhase.ExecutingGoal);

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
				(var returnValues, stepIndex, var stepError) = await RunSteps(goal, context, 0);

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

				var eventError = await HandleGoalError(error, goal, stepIndex, context);
				return eventError;
			}
			finally
			{
				AppContext.SetData("GoalLogLevelByUser", null);
				context.CallStack.ExitGoal();
				goal.Stopwatch.Stop();
				logger.LogDebug($"[End] Goal: {goal.GoalName} => " + goal.Stopwatch.ElapsedMilliseconds);

			}

		}

		private async Task DisposeGoal(Goal goal)
		{
			
		}

		private async Task<(object? ReturnValue, int StepIndex, IError? Error)> RunSteps(Goal goal, PLangContext context, int stepIndex = 0)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			object? returnValues = null;
			IError? error = null;
			logger.LogDebug($"  - Goal {goal.GoalName} starts - {stopwatch.ElapsedMilliseconds}");

			if (context.Callback?.CallbackInfo.GoalHash == goal.Hash)
			{
				stepIndex = context.Callback.CallbackInfo.StepIndex;
				if (stepIndex > goal.GoalSteps.Count - 1)
				{
					return (null, stepIndex, new Error("stepIndex is higher than steps in goal"));
				}
				goal.GoalSteps[stepIndex].Execute = true;
			}

			for (; stepIndex < goal.GoalSteps.Count; stepIndex++)
			{

				Stopwatch stepWatch = Stopwatch.StartNew();
				logger.LogDebug($"   - Step idx {stepIndex} starts - {stepWatch.ElapsedMilliseconds}");

				context.CallStack.SetCurrentStep(goal.GoalSteps[stepIndex], stepIndex);

				logger.LogDebug("   - [S] RunStep:{0} - {1}", goal.GoalSteps[stepIndex].PrFileName, stepWatch.ElapsedMilliseconds);
				(returnValues, error) = await RunStep(goal, stepIndex, context);
				logger.LogDebug("   - [E] RunStep:{0} - {1}", goal.GoalSteps[stepIndex].PrFileName, stepWatch.ElapsedMilliseconds);

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
					var errorInGoalErrorHandler = await HandleGoalError(error, goal, stepIndex, context);
					if (errorInGoalErrorHandler.Error != null) return (returnValues, stepIndex, errorInGoalErrorHandler.Error);
					if (errorInGoalErrorHandler.Error is IErrorHandled) error = null;
				}
				logger.LogDebug($"   - Step idx {stepIndex} done - {stepWatch.ElapsedMilliseconds} - Current for all steps: {stopwatch.ElapsedMilliseconds}");
			}
			logger.LogDebug($"  - All steps done in {goal.GoalName} - {stopwatch.ElapsedMilliseconds}");
			return (returnValues, stepIndex, error);
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

			var result = await RunSteps(goal, context, step.Index);
			return (result.ReturnValue, result.Error);
		}

		private async Task<(object? ReturnValue, IError? Error)> RunStep(Goal goal, int goalStepIndex, PLangContext context, int retryCount = 0)
		{
			var step = goal.GoalSteps[goalStepIndex];
			context.CallStack.SetPhase(ExecutionPhase.ExecutingStep);
			context.CallingStep = step;

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

				var result = await ProcessPrFile(goal, step, goalStepIndex, context);

				if (result.Error != null)
				{
					result = await HandleStepError(goal, step, goalStepIndex, result.Error, retryCount, context);
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
				var result = await HandleStepError(goal, step, goalStepIndex, error, retryCount, context);

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



		private async Task<(object? ReturnValue, IError? Error)> HandleStepError(Goal goal, GoalStep step, int goalStepIndex, IError? error, int retryCount, PLangContext context)
		{
			if (error == null || error is IErrorHandled) return (null, error);
			if (step.ModuleType == "PLang.Modules.ThrowErrorModule" && step.Instruction?.Function.Name == "Throw") return (null, error);

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
							bool isCompleted = true;
							int counter = 0;
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
									engineWait.engine.ParentEngine?.Return(engineWait.engine);
									i--;
									counter++;
									if (counter > 100)
									{
										Console.WriteLine("!!! LOOP waiting on engine");
									}
								}
							}
							if (aliveTaskType.Instances.Count == 0)
							{
								alives.Remove(aliveTaskType);
							}
						}

					}

					EnginePool.CleanupEngines();

				}


			}
		}



		private void WatchForRebuild()
		{
			if (ParentEngine != null) return;

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

								foreach (var item in EnginePool.Pool)
								{
									item.ReloadGoals();
									item.GetEventRuntime().Reload();
								}


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
