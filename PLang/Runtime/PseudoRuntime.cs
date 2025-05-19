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

		public async Task<(IEngine engine, object? Variables, IError? error, IOutput? output)> RunGoal(IEngine engine, PLangAppContext context, string relativeAppPath, GoalToCall goalName,
			Dictionary<string, object?>? parameters, Goal? callingGoal = null,
			bool waitForExecution = true, long delayWhenNotWaitingInMilliseconds = 50, uint waitForXMillisecondsBeforeRunningGoal = 0,
			int indent = 0, bool keepMemoryStackOnAsync = false, bool isolated = false)
		{

			bool isNewEngine = false;
			Goal? goal = null;
			try
			{
				IError? error;
				Stopwatch stopwatch = Stopwatch.StartNew();
				if (goalName == null || goalName.Value == null)
				{
					error = new Error($"Goal to call is empty. Calling goal is {callingGoal}");
					var output2 = new TextOutput("Error", "text/html", false, error, "desktop");
					return (engine, null, error, output2);
				}
				
				ServiceContainer? container = null;
				if (goalName.Value.StartsWith("/"))
				{
					relativeAppPath = "/";
				}
				else
				{
					relativeAppPath = callingGoal?.RelativeGoalFolderPath ?? relativeAppPath;
				}

				string absolutePathToGoal = fileSystem.Path.Join(fileSystem.RootDirectory, relativeAppPath).AdjustPathToOs();
				string goalToRun = fileSystem.Path.Join(relativeAppPath, goalName);
				if (goalToRun.StartsWith("//")) goalToRun = goalToRun.Substring(1);

				
				if (isolated || !waitForExecution || CreateNewContainer(absolutePathToGoal))
				{
					var ms = engine.GetMemoryStack();
					var activeEvents = engine.GetEventRuntime().GetActiveEvents();
					/* todo: this needs to be looked at
					 */
					var engineRootPath = (relativeAppPath.Contains("/apps/")) ? absolutePathToGoal : fileSystem.RootDirectory;

					engine = await engine.GetEnginePool(engineRootPath, null).RentAsync(engineRootPath);
					isNewEngine = true;

					foreach (var item in ms.GetMemoryStack())
					{
						engine.GetMemoryStack().Put(item.Value);
					}
					foreach (var item in context)
					{
						engine.GetContext().AddOrReplace(item.Key, item.Value);
					}
					engine.GetEventRuntime().SetActiveEvents(activeEvents);

					goal = engine.GetGoal(goalToRun);

				}
				else
				{
					goal = engine.GetGoal(goalToRun, callingGoal);
				}


				if (goal == null)
				{
					var goalsAvailable = engine.GetGoalsAvailable(relativeAppPath, goalToRun);
					if (goalsAvailable == null || goalsAvailable.Count == 0)
					{
						var error2 = new Error($"No goals available at {relativeAppPath} trying to run {goalToRun}");
						var output2 = new TextOutput("Error", "text/html", false, error2, "desktop");
						return (engine, null, error2, output2);
					}

					var goals = string.Join('\n', goalsAvailable.OrderBy(p => p.GoalName).Select(p => $" - {p.GoalName} -> Path:{p.RelativeGoalPath}"));
					string strGoalsAvailable = "";
					if (!string.IsNullOrWhiteSpace(goals))
					{
						strGoalsAvailable = $" These goals are available: \n{goals}";

					}

					error = new GoalError($"WARNING! - Goal '{goalName}' at {fileSystem.RootDirectory} was not found.", callingGoal, "GoalNotFound", 500, FixSuggestion: strGoalsAvailable);
					var output3 = new TextOutput("Error", "text/html", false, error, "desktop");
					return (engine, null, error, output3);
				}

				if (waitForExecution)
				{
					goal.ParentGoal = callingGoal;

				}

				var memoryStack = engine.GetMemoryStack();
				context = engine.GetContext();

				if (parameters != null)
				{
					string key;
					foreach (var param in parameters)
					{
						object? value = param.Value;
						key = param.Key.Replace("%", "");
						if (VariableHelper.IsVariable(param.Value))
						{
							value = memoryStack.Get(param.Value?.ToString());
						}
						if (key.StartsWith("!"))
						{
							context.AddOrReplace(key, value);
						}
						else
						{
							memoryStack.Put(key, value);
						}
					}
				}
				var prevIndent = context.GetOrDefault(ReservedKeywords.ParentGoalIndent, 0);
				context.AddOrReplace(ReservedKeywords.ParentGoalIndent, (prevIndent + indent));

				Task<(object? Variables, IError? Error)> task;
				if (waitForExecution)
				{
					task = engine.RunGoal(goal, waitForXMillisecondsBeforeRunningGoal);
					try
					{
						await task;
					}
					catch { }
				}
				else
				{
					task = Task.Run(async () =>
					{
						var stuff = await engine.RunGoal(goal, waitForXMillisecondsBeforeRunningGoal);
						return stuff;
					});

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
				
				error = task.Result.Error;
				if (error is EndGoal endGoal)
				{
					/*
					if (GoalHelper.IsPartOfCallStack(goal, endGoal) && endGoal.Levels == 1)
					{
						error = null;
					}*/

				}

				context.AddOrReplace(ReservedKeywords.ParentGoalIndent, prevIndent);
				//Console.WriteLine($"{space}  Elapsed After Run: {stopwatch.Elapsed}  - {goalToRun}");
				if (task.IsFaulted && task.Exception != null)
				{
					var error3 = new Error(task.Exception.Message, Exception: task.Exception);
					var output3 = new TextOutput("Error", "text/html", false, error3, "desktop");
					return (engine, task.Result.Variables, error3, output3);
				}
				if (waitForExecution)
				{
					return (engine, task.Result.Variables, error, new TextOutput("", "text/html", false, null, "desktop"));
				}
				else
				{
					return (engine, new(), null, new TextOutput("", "text/html", false, null, "desktop"));
				}
			}
			catch (Exception ex)
			{
				return (engine, null, new ExceptionError(ex), null);
			}
			finally
			{
				if (goal != null)
				{
					await goal.DisposeVariables(engine.GetMemoryStack());
				}
				if (isNewEngine)
				{
					engine.GetEnginePool(engine.Path, null).Return(engine);
				}
			}
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
