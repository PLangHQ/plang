using Force.DeepCloner;
using LightInject;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Errors.Handlers;
using PLang.Errors.Runtime;
using PLang.Events;
using PLang.Events.Types;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Models;
using PLang.SafeFileSystem;
using PLang.Services.OutputStream;
using PLang.Utils;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Intrinsics.Arm;
using System.Threading.Tasks;
using static PLang.Modules.BaseBuilder;

namespace PLang.Runtime
{
	public interface IPseudoRuntime
	{
		Task<(IEngine Engine, object? Variables, IError? Error)> RunGoal(IEngine engine, IPLangContextAccessor contextAccessor, string appPath, GoalToCallInfo goalToCall,
			Goal? callingGoal = null, bool waitForExecution = true,
			long delayWhenNotWaitingInMilliseconds = 50, uint waitForXMillisecondsBeforeRunningGoal = 0, int indent = 0,
			bool keepMemoryStackOnAsync = false, bool isolated = false, bool disableOsGoals = false, RuntimeEvent? runtimeEvent = null);
	}

	public class PseudoRuntime : IPseudoRuntime
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly PrParser prParser;
		private readonly ILogger logger;

		public PseudoRuntime(IPLangFileSystem fileSystem, PrParser prParser, ILogger logger)
		{
			this.fileSystem = fileSystem;
			this.prParser = prParser;
			this.logger = logger;
		}

		public async Task<(IEngine Engine, object? Variables, IError? Error)>
			RunGoal(IEngine engine, IPLangContextAccessor contextAccessor, string relativeAppPath, GoalToCallInfo goalToCall, Goal? callingGoal = null,
						bool waitForExecution = true, long delayWhenNotWaitingInMilliseconds = 50,
						uint waitForXMillisecondsBeforeRunningGoal = 0, int indent = 0,
						bool keepMemoryStackOnAsync = false, bool isolated = false, bool disableOsGoals = false, RuntimeEvent? runtimeEvent = null)
		{

			Stopwatch stopwatch = Stopwatch.StartNew();
			logger.LogDebug($"             - Start PseudoRuntime - {stopwatch.ElapsedMilliseconds}");
			if (callingGoal == null)
			{
				callingGoal = prParser.GetAllGoals().FirstOrDefault(p => p.GoalName == "Start");
				if (callingGoal == null) return (engine, null, new Error($"Calling goal is null. {ErrorReporting.CreateIssueShouldNotHappen}"));
			}

			var goalName = goalToCall.Name;
			var parameters = goalToCall.Parameters;
			var isRented = false;

			var goals = prParser.GetGoals();
			var systemGoals = (disableOsGoals) ? new List<Goal>() : prParser.GetSystemGoals();

			var relativeGoalPath = callingGoal.RelativeGoalPath;
			var appStartFolderPath = callingGoal.AbsoluteAppStartupFolderPath;
			if (runtimeEvent?.SourceStep != null)
			{
				relativeGoalPath = runtimeEvent.SourceStep.RelativeGoalPath;
			}
			
			(var goalToRun, var error) = GoalHelper.GetGoal(relativeGoalPath, fileSystem.RootDirectory, goalToCall, goals, systemGoals);
			if (goalToCall.Name == "CreateUserInVitalSource")
			{
				Console.WriteLine($"CreateUserInVitalSource: {goalToCall.Name} | params:{string.Join(",", goalToCall.Parameters.Select(p => p.Key))} | path: {goalToCall.Path}");
				if (goalToRun != null)
				{
					Console.WriteLine($"Goal: {goalToRun.GoalName} | pr:{goalToRun.RelativePrPath}");
				} else
				{
					Console.WriteLine($"Goal IS NULL: relativeGoalPath:{relativeGoalPath} | fileSystem.RootDirectory: {fileSystem.RootDirectory} | goalToCall:{goalToCall}");
					throw new Exception("CreateUserInVitalSource Goal is null");
				}

			}
			if (error != null) return (engine, null, error);
			if (goalToRun == null) return (engine, null, new Error($"{goalToCall.Name} could not be found"));

			var runtimeEngine = engine;
			var context = contextAccessor.Current;


			logger.LogDebug($"             - Have goal - {stopwatch.ElapsedMilliseconds}");
			try
			{

				// todo: (Decision) The idea behind isolation is when you call a external app, that app should not have access
				// to the memory of the calling app, and only get the parameters that are sent
				// this is not working now, when I rent engine it gets the memory.
				// this might not be an issues since all goals are open source and can be easily validated
				// decision: leave it in to give memory stack to isolated goals
				if (isolated || !waitForExecution || CreateNewContainer(goalToRun.AbsoluteGoalFolderPath))
				{
					isRented = true;

					runtimeEngine = await engine.RentAsync(context.CallingStep);
					logger.LogDebug($"             - Rented engine - {stopwatch.ElapsedMilliseconds}");
				}

				if (runtimeEvent != null)
				{
					goalToRun.Event = runtimeEvent;
					context.Event = runtimeEvent;
				}

				// prevent loop reference
				if (callingGoal.ParentGoal == null || !callingGoal.ParentGoal.RelativePrPath.Equals(goalToRun.RelativePrPath))
				{
					goalToRun.ParentGoal = callingGoal;
				}

				var memoryStack = context.MemoryStack;

				if (parameters != null)
				{
					foreach (var param in parameters ?? [])
					{
						object? value = (param.Value is ObjectValue ov) ? ov.Value : param.Value;
						if (param.Key.StartsWith("!"))
						{
							context.AddVariable(value, variableName: param.Key);
						}
						else
						{
													
							memoryStack.Put(param.Key, value, goalStep: context.CallingStep, disableEvent: true);
						}
					}
				}
				var prevIndent = context.GetVariable<int?>(ReservedKeywords.ParentGoalIndent) ?? 0;
				context.AddVariable((prevIndent + indent), variableName: ReservedKeywords.ParentGoalIndent);
				logger.LogDebug($"             - Running goal waitForExecution:{waitForExecution} - {stopwatch.ElapsedMilliseconds}");
				Task<(object? Variables, IError? Error)> task;
				if (waitForExecution)
				{
					task = runtimeEngine.RunGoal(goalToRun, context, waitForXMillisecondsBeforeRunningGoal);
					try
					{
						await task;
					}
					catch { }

					if (task.IsFaulted && task.Exception != null)
					{
						error = new ExceptionError(task.Exception, task.Exception.Message, callingGoal, context.CallingStep);
					}
					else
					{
						error = task.Result.Error;
					}

					return (engine, task.Result.Variables, error);
				}
				else
				{
					
					logger.LogDebug($"               - Create new memory for Task.Run {goalToRun.GoalName} - {stopwatch.ElapsedMilliseconds}");
					var newMemoryStack = MemoryStack.New(runtimeEngine.Container, runtimeEngine);
					foreach (var item in memoryStack.GetMemoryStack())
					{
						try
						{
							if (item.Name == "!Callstack") continue;

							logger.LogDebug($"                 - Deep clone on {item.Name} {goalToRun.GoalName} - {stopwatch.ElapsedMilliseconds}");
							newMemoryStack.Put(new ObjectValue(item.Name, item.Value.DeepClone()));

							
						}
						catch (Exception ex)
						{
							throw;
						}
					}
					logger.LogDebug($"               - Task.Run {goalToRun.GoalName} - {stopwatch.ElapsedMilliseconds}");
					task = Task.Run(async () =>
					{
						try
						{

							logger.LogDebug($"             - Starting to clone context {goalToRun.GoalName} - {stopwatch.ElapsedMilliseconds}");
							var newContext = context.Clone(newMemoryStack, runtimeEngine);
							newContext.IsAsync = true;
							newContext.HttpContext = null;
							contextAccessor.Current = newContext;
							
							logger.LogDebug($"             - Done cloning context {goalToRun.GoalName} - {stopwatch.ElapsedMilliseconds}");
							var msa = runtimeEngine.Container.GetInstance<IMemoryStackAccessor>();
							msa.Current = newMemoryStack;
							logger.LogDebug($"             - Have new memory, now runtimeEngine.RunGoal {goalToRun.GoalName} - {stopwatch.ElapsedMilliseconds}");
							var (variables, error) = await runtimeEngine.RunGoal(goalToRun, newContext, waitForXMillisecondsBeforeRunningGoal);
							if (error != null && error is not EndGoal)
							{
								(_, error) = await runtimeEngine.GetEventRuntime().AppErrorEvents(error);

								if (error != null)
								{
									Console.WriteLine("Error running async goal:" + error.ToString());					
									
								}

							}
							logger.LogDebug($"             - Done Running Task goal {goalToRun.GoalName} - {stopwatch.ElapsedMilliseconds}");
							return (variables, error);
						} finally
						{
							if (isRented)
							{
								engine.Return(runtimeEngine);
							}
						}
						
					});
				
					KeepAlive(engine, task);

					return (runtimeEngine, task, null);
				}


			}
			catch (Exception ex)
			{
				return (engine, null, new ExceptionError(ex));
			}
			finally
			{
				IError? disposeError = null;
				if (goalToRun != null && !isRented)
				{
					//disposeError = await context.CallStack.CurrentFrame.DisposeVariables(context.MemoryStack);
								
				}
				if (isRented && waitForExecution)
				{
					engine.Return(runtimeEngine);
				}

				if (disposeError != null)
				{ 
					throw new ExceptionWrapper(disposeError);
				}
			}
		}

		private static void KeepAlive(IEngine engine, Task<(object? Variables, IError? Error)> task)
		{
			var alives = engine.GetAppContext().GetOrDefault<List<Alive>>("KeepAlive");
			if (alives == null) alives = new List<Alive>();

			var aliveType = alives.FirstOrDefault(p => p.Type == task.GetType() && p.Key == "WaitForExecution");
			if (aliveType == null)
			{
				aliveType = new Alive(task.GetType(), "WaitForExecution", [new EngineWait(task, engine)]);
				alives.Add(aliveType);

				engine.GetAppContext().AddOrReplace("KeepAlive", alives);
			}
			else
			{
				aliveType.Instances!.Add(new EngineWait(task, engine));
			}
		}


		public record EngineWait(Task task, IEngine engine);
		public (string absolutePath, GoalToCallInfo goalName) GetAppAbsolutePath(string absolutePathToGoal, GoalToCallInfo? goalName = null)
		{
			absolutePathToGoal = absolutePathToGoal.AdjustPathToOs();

			Dictionary<string, int> dict = new Dictionary<string, int>();
			dict.Add("apps", absolutePathToGoal.LastIndexOf("apps"));
			dict.Add(".modules", absolutePathToGoal.LastIndexOf(".modules"));
			dict.Add(".services", absolutePathToGoal.LastIndexOf(".services"));

			var item = dict.OrderByDescending(p => p.Value).FirstOrDefault();
			if (item.Value == -1) return (absolutePathToGoal, goalName);

			var idx = absolutePathToGoal.IndexOf(fileSystem.Path.DirectorySeparatorChar, item.Value + item.Key.Length + 1);
			if (idx == -1)
			{
				idx = absolutePathToGoal.IndexOf(fileSystem.Path.DirectorySeparatorChar, item.Value + item.Key.Length);
			}

			var absolutePathToApp = absolutePathToGoal.Substring(0, idx);
			foreach (var itemInDict in dict)
			{
				if (absolutePathToApp.EndsWith(itemInDict.Key))
				{
					absolutePathToApp = absolutePathToGoal;
				}
			}
			if (absolutePathToApp.EndsWith(fileSystem.Path.DirectorySeparatorChar))
			{
				absolutePathToApp = absolutePathToApp.TrimEnd(fileSystem.Path.DirectorySeparatorChar);
			}

			goalName = absolutePathToGoal.Replace(absolutePathToApp, "").TrimStart(fileSystem.Path.DirectorySeparatorChar);
			if (string.IsNullOrEmpty(goalName)) goalName = "Start";

			return (absolutePathToApp, goalName);
		}

		private bool CreateNewContainer(string absoluteGoalPath)
		{
			string servicesFolder = fileSystem.Path.Join(fileSystem.RootDirectory, ".services");
			string modulesFolder = fileSystem.Path.Join(fileSystem.RootDirectory, ".modules");
			string appsFolder = fileSystem.Path.Join(fileSystem.RootDirectory, "apps");
			return absoluteGoalPath.StartsWith(servicesFolder) || absoluteGoalPath.StartsWith(modulesFolder) || absoluteGoalPath.StartsWith(appsFolder);
		}




	}


}
