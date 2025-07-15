using LightInject;
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
using PLang.Models;
using PLang.SafeFileSystem;
using PLang.Services.OutputStream;
using PLang.Utils;
using System.Diagnostics;
using System.Threading.Tasks;
using static PLang.Modules.BaseBuilder;

namespace PLang.Runtime
{
	public interface IPseudoRuntime
	{
		Task<(IEngine engine, object? Variables, IError? error)> RunGoal(IEngine engine, PLangAppContext context, string appPath, GoalToCallInfo goalToCall,
			Goal? callingGoal = null, bool waitForExecution = true,
			long delayWhenNotWaitingInMilliseconds = 50, uint waitForXMillisecondsBeforeRunningGoal = 0, int indent = 0,
			bool keepMemoryStackOnAsync = false, bool isolated = false, bool disableOsGoals = false, bool isEvent = false);
	}

	public class PseudoRuntime : IPseudoRuntime
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly PrParser prParser;

		public PseudoRuntime(IPLangFileSystem fileSystem, PrParser prParser)
		{
			this.fileSystem = fileSystem;
			this.prParser = prParser;
		}

		public async Task<(IEngine engine, object? Variables, IError? error)>
			RunGoal(IEngine engine, PLangAppContext context, string relativeAppPath, GoalToCallInfo goalToCall, Goal? callingGoal = null,
						bool waitForExecution = true, long delayWhenNotWaitingInMilliseconds = 50,
						uint waitForXMillisecondsBeforeRunningGoal = 0, int indent = 0,
						bool keepMemoryStackOnAsync = false, bool isolated = false, bool disableOsGoals = false, bool isEvent = false)
		{

			if (callingGoal == null) return (engine, null, new Error($"Calling goal is null. {ErrorReporting.CreateIssueShouldNotHappen}"));

			var goalName = goalToCall.Name;
			var parameters = goalToCall.Parameters;
			var isRented = false;

			var goals = prParser.GetGoals();
			var systemGoals = (disableOsGoals) ? new() : prParser.GetSystemGoals();

			var callingStep = (callingGoal.CurrentStepIndex != -1) ? callingGoal.GoalSteps[callingGoal.CurrentStepIndex] : null;
			var relativeGoalPath = callingGoal.RelativeGoalPath;
			var appStartFolderPath = callingGoal.AbsoluteAppStartupFolderPath;
			
			// todo: hack, should not be modifying like this
			if (isEvent)
			{
				var eventBinding = goalToCall.Parameters[ReservedKeywords.Event] as EventBinding;
				if (eventBinding != null)
				{
					relativeGoalPath = eventBinding.Goal!.RelativeGoalPath;
				}
			}
			(var goalToRun, var error) = GoalHelper.GetGoal(relativeGoalPath, fileSystem.RootDirectory, goalToCall, goals, systemGoals);
			
			if (error != null) return (engine, null, error);
			if (goalToRun == null) return (engine, null, new Error($"{goalToCall.Name} could not be found"));

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
					
					engine = await engine.GetEnginePool(fileSystem.RootDirectory).RentAsync(engine, callingStep, fileSystem.RootDirectory);
				}


				goalToRun.IsEvent = isEvent;

				// prevent loop reference
				if (callingGoal.ParentGoal == null || !callingGoal.ParentGoal.RelativePrPath.Equals(goalToRun.RelativePrPath))
				{
					goalToRun.ParentGoal = callingGoal;
				}

				var memoryStack = engine.GetMemoryStack();

				if (parameters != null)
				{
					foreach (var param in parameters ?? [])
					{
						if (param.Key.StartsWith("!"))
						{
							goalToRun.AddVariable(param.Value, variableName: param.Key);
						}
						else
						{
							memoryStack.Put(param.Key, param.Value, goalStep: callingStep);
						}
					}
				}
				var prevIndent = goalToRun.GetVariable<int?>(ReservedKeywords.ParentGoalIndent) ?? 0;
				goalToRun.AddVariable((prevIndent + indent), variableName: ReservedKeywords.ParentGoalIndent);

				Task<(object? Variables, IError? Error)> task;
				if (waitForExecution)
				{
					task = engine.RunGoal(goalToRun, waitForXMillisecondsBeforeRunningGoal);
					try
					{
						await task;
					}
					catch { }

					if (task.IsFaulted && task.Exception != null)
					{
						error = new ExceptionError(task.Exception, task.Exception.Message, callingGoal, callingStep);
					}
					else
					{
						error = task.Result.Error;
					}

					return (engine, task.Result.Variables, error);
				}
				else
				{
					task = Task.Run(async () =>
					{
						try
						{
							var result = await engine.RunGoal(goalToRun, waitForXMillisecondsBeforeRunningGoal);
							return result;
						} catch
						{
							throw;
						} finally
						{
							engine.ParentEngine?.GetEnginePool(engine.Path).Return(engine);
						}
						
					});
					
					KeepAlive(engine, task);

					return (engine, task, null);
				}


			}
			/*
			catch (FileAccessException ex)
			{
				return (engine, null, new AskUserFileAccess(ex.AppName, ex.Path, ex.Message, async (appName, path, answer) =>
				{
					
					
					fileSystem.AddFileAccess(new FileAccessControl(appName, path, ProcessId: engine.Id));

					var result = await RunGoal(engine, context, relativeAppPath, goalToCall, callingGoal,
						waitForExecution, delayWhenNotWaitingInMilliseconds,
						waitForXMillisecondsBeforeRunningGoal, indent,
						keepMemoryStackOnAsync, isolated);
					if (result.error != null) return (false, result.error);

					return (true, null);
				}), null);
			}*/
			catch (Exception ex)
			{
				return (engine, null, new ExceptionError(ex));
			}
			finally
			{
				if (goalToRun != null)
				{
					await goalToRun.DisposeVariables(engine.GetMemoryStack());
				}
				if (isRented && waitForExecution)
				{
					engine.ParentEngine?.GetEnginePool(engine.Path).Return(engine);
				}
			}
		}

		private static void KeepAlive(IEngine engine, Task<(object? Variables, IError? Error)> task)
		{
			var alives = AppContext.GetData("KeepAlive") as List<Alive>;
			if (alives == null) alives = new List<Alive>();

			var aliveType = alives.FirstOrDefault(p => p.Type == task.GetType() && p.Key == "WaitForExecution");
			if (aliveType == null)
			{
				aliveType = new Alive(task.GetType(), "WaitForExecution", [new EngineWait(task, engine)]);
				alives.Add(aliveType);

				AppContext.SetData("KeepAlive", alives);
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
