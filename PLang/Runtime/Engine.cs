using LightInject;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building;
using PLang.Building.Events;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Modules;
using PLang.Modules.HtmlModule;
using PLang.SafeFileSystem;
using PLang.Services.OutputStream;
using PLang.Services.SettingsService;
using PLang.Utils;
using System.Diagnostics;
using System.Net;

namespace PLang.Runtime
{
    public interface IEngine
	{
		IOutputStream OutputStream { get; }
		void AddContext(string key, object value);
		PLangAppContext GetContext();
		MemoryStack GetMemoryStack();
		void Init(IServiceContainer container);
		Task Run(List<string> goalsToRun);
		Task RunGoal(Goal goal);
		public HttpListenerContext? HttpContext { get; set; }
	}
	public record Alive(Type Type, string Key);
	public class Engine : IEngine
	{
		private IServiceContainer container;

		private IPLangFileSystem fileSystem;
		private ILogger logger;
		private ISettings settings;
		private IEventRuntime eventRuntime;
		private ITypeHelper typeHelper;
		private IErrorHelper errorHelper;
		public IOutputStream OutputStream { get; private set; }

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
			this.logger = container.GetInstance<ILogger>();
			this.settings = container.GetInstance<ISettings>();
			this.eventRuntime = container.GetInstance<IEventRuntime>();
			this.typeHelper = container.GetInstance<ITypeHelper>();
			this.errorHelper = container.GetInstance<IErrorHelper>();

			this.OutputStream = container.GetInstance<IOutputStream>();
			this.prParser = container.GetInstance<PrParser>();
			this.memoryStack = container.GetInstance<MemoryStack>();


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
				if (goalsToRun.Count == 1 && Path.GetFileNameWithoutExtension(goalsToRun[0].ToLower()) == "setup") return;

				StartScheduler();

				await RunStart(goalsToRun);
				await eventRuntime.RunStartEndEvents(context, EventType.After, EventScope.StartOfApp);

			}
			catch (FileAccessException ex)
			{
				var fileAccessHandler = new FileAccessHandler(settings, container.GetInstance<ILlmService>(), logger, fileSystem);
				var askUserFileException = new AskUserFileAccess(ex.AppName, ex.Path, ex.Message, fileAccessHandler.ValidatePathResponse);

				var askUserHandler = container.GetInstance<IAskUserHandler>(context[ReservedKeywords.Inject_AskUserHandler].ToString());
				if (await askUserHandler.Handle(askUserFileException))
				{
					await Run(goalsToRun);
				}
			}
			catch (Exception ex)
			{
				context.AddOrReplace(ReservedKeywords.Exception, ex);
				await eventRuntime.RunStartEndEvents(context, EventType.OnError, EventScope.RunningApp);

				if (context.ContainsKey(ReservedKeywords.Exception) && context[ReservedKeywords.Exception] != null)
				{
					await errorHelper.ShowFriendlyErrorMessage(ex);
				}
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

		private void StartScheduler()
		{
			var containerForScheduler = new ServiceContainer();
			containerForScheduler.RegisterForPLangConsole(settings.GoalsPath, "\\");

			var schedulerEngine = containerForScheduler.GetInstance<IEngine>();
			schedulerEngine.Init(containerForScheduler);
			Modules.ScheduleModule.Program.Start(containerForScheduler.GetInstance<ISettings>(),
				schedulerEngine, containerForScheduler.GetInstance<PrParser>(),
				containerForScheduler.GetInstance<ILogger>(), containerForScheduler.GetInstance<IPseudoRuntime>(),
				containerForScheduler.GetInstance<IPLangFileSystem>());
		}

		private async Task RunSetup()
		{

			string setupFolder = Path.Combine(settings.BuildPath, "Setup");
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
					logger.LogWarning($"Could not find Start.goal to run. Are you in correct directory? I am running from {settings.GoalsPath}. If you want to run specific goal file, for example Test.goal, you must run it like this: 'plang run Test'");
				} else
				{
					logger.LogWarning($"Goal file(s) not found to run. Are you in correct directory? I am running from {settings.GoalsPath}");
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

		public async Task RunGoal(Goal goal)
		{
			context.AddOrReplace(ReservedKeywords.Goal, goal);
			if (goal.Comment != null && goal.Comment.ToLower().Contains("[trace]"))
			{
				AppContext.SetData("GoalLogLevelByUser", Microsoft.Extensions.Logging.LogLevel.Trace);
			}

			int goalStepIndex = 0;
			try
			{
				bool isSetup = GoalFiles.IsSetup(fileSystem.RootDirectory, goal.AbsoluteGoalPath);
				logger.LogTrace("RootDirectory:{0}", fileSystem.RootDirectory);
				foreach (var injection in goal.Injections)
				{
					container.RegisterForPLangUserInjections(injection.Type, injection.Path, injection.IsGlobal);
				}

				logger.LogDebug("Goal: " + goal.GoalName);
				await eventRuntime.RunGoalEvents(context, EventType.Before, goal);

				for (; goalStepIndex < goal.GoalSteps.Count; goalStepIndex++)
				{
					try
					{
						await RunStep(goal, goalStepIndex);
					}
					catch (Exception ex) when (!(ex is RuntimeUserStepException || ex is RuntimeGoalEndException))
					{
						await errorHelper.ShowFriendlyErrorMessage(ex,
							callBackForAskUser: async () =>
							{
								await RunStep(goal, goalStepIndex);
							},
							eventToRun: async (Exception ex) =>
							{
								context.AddOrReplace(ReservedKeywords.Exception, ex);
								await eventRuntime.RunStepEvents(context, EventType.OnError, goal, goal.GoalSteps[goalStepIndex]);
								return false;
							}
						);
					}
				}

				await eventRuntime.RunGoalEvents(context, EventType.After, goal);

				// Cleanup any context settings that each step is reponsible for so it doesnt transfer to other.
				if (context.ContainsKey("DisposableSteps"))
				{
					var disposableSteps = context["DisposableSteps"] as List<IFlush>;
					foreach (var step in disposableSteps)
					{
						//step.Flush();
					}
					context.Remove("DisposableSteps");
				}
			}
			catch (RuntimeUserStepException rse)
			{
				await OutputStream.Write(rse.Message, rse.Type, rse.StatusCode);
			}
			catch (RuntimeGoalEndException ex)
			{
				//this equals to doing return in a function
				logger.LogDebug(ex.Message, ex);
			}
			catch (Exception? ex)
			{
				await errorHelper.ShowFriendlyErrorMessage(ex,
							callBackForAskUser: async () =>
							{
								await RunGoal(goal);
							},
							eventToRun: async (Exception ex) =>
							{
								context.AddOrReplace(ReservedKeywords.Exception, ex);
								await eventRuntime.RunGoalEvents(context, EventType.OnError, goal);
								return false;
							}
						);

			}
			finally
			{
				if (goal.Comment != null && goal.Comment.ToLower().Contains("[trace]"))
				{
					AppContext.SetData("GoalLogLevelByUser", null);
				}
			}

		}

		private async Task RunStep(Goal goal, int goalStepIndex)
		{

			if (goal.GoalSteps[goalStepIndex].Execute && goal.GoalSteps[goalStepIndex].Executed == null)
			{
				await eventRuntime.RunStepEvents(context, EventType.Before, goal, goal.GoalSteps[goalStepIndex]);
			}

			logger.LogDebug($"- {goal.GoalSteps[goalStepIndex].Text.Replace("\n", "").Replace("\r", "").MaxLength(80)}");

			await ProcessPrFile(goal, goal.GoalSteps[goalStepIndex], goalStepIndex);
			if (goal.GoalSteps[goalStepIndex].Execute && goal.GoalSteps[goalStepIndex].Executed == null)
			{
				await eventRuntime.RunStepEvents(context, EventType.After, goal, goal.GoalSteps[goalStepIndex]);
			}


		}


		public async Task ProcessPrFile(Goal goal, GoalStep goalStep, int stepIndex, int currentRetryCount = 0)
		{
			if (goalStep.RunOnce && goalStep.Executed != null) return;

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


			var classInstance = container.GetInstance(classType) as BaseProgram;
			if (classInstance == null) return;

			try
			{
				var llmService = container.GetInstance<ILlmService>();
				var appCache = container.GetInstance<IAppCache>(context[ReservedKeywords.Inject_Caching].ToString());

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
					catch (RunGoalException goalException)
					{
						var goalName = goalException.Message;
						var goalToCall = prParser.GetGoalByAppAndGoalName(goal.AbsoluteAppStartupFolderPath, goalName, goal);
						if (goalToCall != null)
						{
							var ex = goalException.InnerException;
							if (ex.Message.StartsWith("One or more errors occurred.") && ex.InnerException != null)
							{
								ex = ex.InnerException;
							}
							context.AddOrReplace(ReservedKeywords.Exception, ex);
							await RunGoal(goalToCall);
							
							//stop running any other steps
							stepIndex = goal.GoalSteps.Count;
						}
						else
						{
							logger.LogError($"Could not find {goalName} to call");
							throw;
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
					catch (Exception ex)
					{
						throw;
					}
					finally
					{
						// reeset the Execute to false on all steps inside if statement
						if (goalStep.Indent > 0)
						{
							goalStep.Execute = false;
						}
					}
				}


			}
			catch (RuntimeUserStepException) { throw; }
			catch (RuntimeGoalEndException) { throw; }
			catch (Exception ex)
			{
				await errorHelper.ShowFriendlyErrorMessage(ex, goalStep,
					callBackForAskUser: async () =>
					{
						await ProcessPrFile(goal, goalStep, stepIndex, 0);
					},

					retryCallback: async (task) =>
					{
						if (goalStep.RetryHandler != null && goalStep.RetryHandler.RetryCount > currentRetryCount)
						{
							await Task.Delay(goalStep.RetryHandler.RetryDelayInMilliseconds);
							await ProcessPrFile(goal, goalStep, stepIndex, ++currentRetryCount);
							return true;
						}
						return false;
					},

					eventToRun: async (Exception ex) =>
					{

						if (goal.GoalSteps[stepIndex].ErrorHandler == null
							|| goal.GoalSteps[stepIndex].ErrorHandler.OnExceptionContainingTextCallGoal == null) return false;

						if (goal.GoalSteps[stepIndex].ErrorHandler.IgnoreErrors) return true;
						if (ex.InnerException == null) return false;

						var errorString = ex.InnerException.Message.ToString().ToLower();
						var errorDictionary = goal.GoalSteps[stepIndex].ErrorHandler.OnExceptionContainingTextCallGoal;

						foreach (var item in errorDictionary)
						{
							if (errorString.Contains(item.Key))
							{
								var goalName = item.Value.Replace("!", "").ToLower();
								var goalToRun = prParser.GetAllGoals().FirstOrDefault(p => p.RelativePrFolderPath == goal.RelativePrFolderPath && p.GoalName.ToLower() == goalName);
								if (goalToRun == null)
								{
									goalToRun = prParser.GetAllGoals().FirstOrDefault(p => p.GoalName.ToLower() == goalName);
								}
								if (goalToRun != null)
								{
									await RunGoal(goalToRun);
									return true;
								}
								logger.LogError($"Could not find goal {item.Value} to run on error {item.Key}");
								return false;

							}
						}
						return false;

					}
				);

			}

		}


		private List<string> GetStartGoals(List<string> goalNames)
		{
			List<string> goalsToRun = new();
			if (goalNames.Count > 0)
			{
				var goalFiles = fileSystem.Directory.GetFiles(settings.BuildPath, ISettings.GoalFileName, SearchOption.AllDirectories).ToList();
				foreach (var goalName in goalNames)
				{
					if (string.IsNullOrEmpty(goalName)) continue;

					string name = goalName.AdjustPathToOs().Replace(".goal", "").ToLower();
					if (name.StartsWith(".")) name = name.Substring(1);

					var folderPath = goalFiles.FirstOrDefault(p => p.ToLower().EndsWith(Path.Join(name, ISettings.GoalFileName).ToLower()));
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
			
			if (!fileSystem.Directory.Exists(Path.Join(settings.BuildPath, "Start"))) {
				return new();
			}

			var startFile = fileSystem.Directory.GetFiles(Path.Join(settings.BuildPath, "Start"), ISettings.GoalFileName, SearchOption.AllDirectories).FirstOrDefault();
			if (startFile != null)
			{
				goalsToRun.Add(startFile);
				return goalsToRun;
			}

			var files = fileSystem.Directory.GetFiles(settings.GoalsPath, "*.goal");
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
				logger.LogError($"No goal file could be found in directory. Are you in the correct directory? I am running from {settings.GoalsPath}");
				return new();
			}

			return goalsToRun;

		}



	}
}
