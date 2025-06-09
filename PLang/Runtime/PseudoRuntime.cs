using LightInject;
using PLang.Building.Model;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Handlers;
using PLang.Errors.Runtime;
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
		Task<(IEngine engine, object? Variables, IError? error, IOutput? output)> RunGoal(IEngine engine, PLangAppContext context, string appPath, GoalToCall goalName,
			Dictionary<string, object?>? parameters, Goal? callingGoal = null, bool waitForExecution = true,
			long delayWhenNotWaitingInMilliseconds = 50, uint waitForXMillisecondsBeforeRunningGoal = 0, int indent = 0,
			bool keepMemoryStackOnAsync = false, bool isolated = false);
	}

	public class PseudoRuntime : IPseudoRuntime
	{
		private readonly IPLangFileSystem fileSystem;

		public PseudoRuntime(IPLangFileSystem fileSystem)
		{
			this.fileSystem = fileSystem;
		}

		public async Task<(IEngine engine, object? Variables, IError? error, IOutput? output)>
			RunGoal(IEngine engine, PLangAppContext context, string relativeAppPath, GoalToCall goalName,
						Dictionary<string, object?>? parameters, Goal? callingGoal = null,
						bool waitForExecution = true, long delayWhenNotWaitingInMilliseconds = 50,
						uint waitForXMillisecondsBeforeRunningGoal = 0, int indent = 0,
						bool keepMemoryStackOnAsync = false, bool isolated = false)
		{

			Goal? goalToRun = null;
			try
			{
				IError? error;

				if (goalName == null || goalName.Value == null)
				{
					error = new Error($"Goal to call is empty. This is not allowed. Calling goal is {callingGoal}") {  Goal = callingGoal };
					var output2 = new TextOutput("Error", "text/html", false, error, "desktop");
					return (engine, null, error, output2);
				}

				if (goalName.Value.StartsWith("/"))
				{
					relativeAppPath = "/";
				}
				else
				{
					relativeAppPath = callingGoal?.RelativeGoalFolderPath ?? relativeAppPath;
				}

				string absolutePathToGoal = fileSystem.Path.Join(fileSystem.RootDirectory, relativeAppPath).AdjustPathToOs();
				string goalToRunPath = fileSystem.Path.Join(relativeAppPath, goalName);
				if (goalToRunPath.StartsWith("//")) goalToRunPath = goalToRunPath.Substring(1);

				// todo: (Decision) The idea behind isolation is when you call a external app, that app should not have access
				// to the memory of the calling app, and only get the parameters that are sent
				// this is not working now, when I rent engine it gets the memory.
				// this might not be an issues since all goals are open source and can be easily validated
				// decision: leave it in to give memory stack to isolated goals
				if (isolated || !waitForExecution || CreateNewContainer(absolutePathToGoal))
				{

					goalToRun = engine.GetGoal(goalToRunPath);
					if (goalToRun == null) return GoalToRunIsNull(engine, relativeAppPath, goalName, callingGoal, goalToRunPath);

					var engineRootPath = (relativeAppPath.Contains("/apps/")) ? absolutePathToGoal : fileSystem.RootDirectory;
					if (goalToRun.IsOS)
					{
						engineRootPath = fileSystem.OsDirectory;
					}

					GoalStep? callingStep = null;
					if (callingGoal != null)
					{
						callingStep = callingGoal.GoalSteps[callingGoal.CurrentStepIndex];
						//return (engine, null, new Error($"calling goal cannot be empty.{ErrorReporting.CreateIssueShouldNotHappen}"), null);
					}

					engine = await engine.GetEnginePool(engineRootPath).RentAsync(engine, callingStep, engineRootPath);
				}
				else
				{
					goalToRun = engine.GetGoal(goalToRunPath, callingGoal);
				}


				if (goalToRun == null)
				{
					return GoalToRunIsNull(engine, relativeAppPath, goalName, callingGoal, goalToRunPath);
				}

				if (waitForExecution)
				{
					goalToRun.ParentGoal = callingGoal;

				}

				var memoryStack = engine.GetMemoryStack();
				GoalStep? goalStep = (callingGoal != null) ? callingGoal.GoalSteps[callingGoal.CurrentStepIndex] : null;

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
							memoryStack.Put(param.Key, param.Value, goalStep: goalStep);
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
						error = new ExceptionError(task.Exception, task.Exception.Message, callingGoal, goalStep);
					}
					else
					{
						error = task.Result.Error;
					}

					return (engine, task.Result.Variables, error, new TextOutput("", "text/html", false, null, "desktop"));
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

					return (engine, null, null, new TextOutput("", "text/html", false, null, "desktop"));
				}


			}
			catch (Exception ex)
			{
				return (engine, null, new ExceptionError(ex), null);
			}
			finally
			{
				if (goalToRun != null)
				{
					await goalToRun.DisposeVariables(engine.GetMemoryStack());
				}
				if (waitForExecution && engine.ParentEngine != null)
				{
					engine.ParentEngine.GetEnginePool(engine.Path).Return(engine);
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

		private (IEngine engine, object? Variables, IError? error, IOutput? output) GoalToRunIsNull(IEngine engine, string relativeAppPath,
				GoalToCall goalName, Goal? callingGoal, string goalToRunPath)
		{
			var goalsAvailable = engine.GetGoalsAvailable(relativeAppPath, goalToRunPath);
			if (goalsAvailable == null || goalsAvailable.Count == 0)
			{
				var error2 = new Error($"No goals available at {relativeAppPath} trying to run {goalToRunPath}");
				var output2 = new TextOutput("Error", "text/html", false, error2, "desktop");
				return (engine, null, error2, output2);
			}

			var goals = string.Join('\n', goalsAvailable.OrderBy(p => p.GoalName).Select(p => $" - {p.GoalName} -> Path:{p.RelativeGoalPath}"));
			string strGoalsAvailable = "";
			if (!string.IsNullOrWhiteSpace(goals))
			{
				strGoalsAvailable = $" These goals are available: \n{goals}";

			}

			var error = new GoalError($"WARNING! - Goal '{goalName}' at {fileSystem.RootDirectory} was not found.", callingGoal, "GoalNotFound", 500, FixSuggestion: strGoalsAvailable);
			var output3 = new TextOutput("Error", "text/html", false, error, "desktop");
			return (engine, null, error, output3);
		}

		public record EngineWait(Task task, IEngine engine);
		public (string absolutePath, GoalToCall goalName) GetAppAbsolutePath(string absolutePathToGoal, GoalToCall? goalName = null)
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
