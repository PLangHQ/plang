using LightInject;
using PLang.Building.Model;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Handlers;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Models;
using PLang.SafeFileSystem;
using PLang.Services.OutputStream;
using PLang.Utils;

namespace PLang.Runtime
{
	public interface IPseudoRuntime
	{
		Task<(IEngine engine, IError? error, IOutput output)> RunGoal(IEngine engine, PLangAppContext context, string appPath, GoalToCall goalName, 
			Dictionary<string, object?>? parameters, Goal? callingGoal = null, bool waitForExecution = true, 
			long delayWhenNotWaitingInMilliseconds = 50, uint waitForXMillisecondsBeforeRunningGoal = 0, int indent = 0, 
			bool keepMemoryStackOnAsync = false, bool isolated = false	);
	}

	public class PseudoRuntime : IPseudoRuntime
	{
		private readonly IServiceContainerFactory serviceContainerFactory;
		private readonly IPLangFileSystem fileSystem;
		private readonly IOutputStreamFactory outputStreamFactory;
		private readonly IOutputSystemStreamFactory outputSystemStreamFactory;
		private readonly IErrorHandlerFactory errorHandlerFactory;
		private readonly IErrorSystemHandlerFactory errorSystemHandlerFactory;
		private readonly IAskUserHandlerFactory askUserHandlerFactory;
		
		public PseudoRuntime(IServiceContainerFactory serviceContainerFactory, IPLangFileSystem fileSystem,
			IOutputStreamFactory outputStreamFactory, IOutputSystemStreamFactory outputSystemStreamFactory, 
			IErrorHandlerFactory errorHandlerFactory, IErrorSystemHandlerFactory errorSystemHandlerFactory,
			IAskUserHandlerFactory askUserHandlerFactory)
		{
			this.serviceContainerFactory = serviceContainerFactory;
			this.fileSystem = fileSystem;
			this.outputStreamFactory = outputStreamFactory;
			this.outputSystemStreamFactory = outputSystemStreamFactory;
			this.errorHandlerFactory = errorHandlerFactory;
			this.errorSystemHandlerFactory = errorSystemHandlerFactory;
			this.askUserHandlerFactory = askUserHandlerFactory;
		}

		public async Task<(IEngine engine, IError? error, IOutput? output)> RunGoal(IEngine engine, PLangAppContext context, string relativeAppPath, GoalToCall goalName, 
			Dictionary<string, object?>? parameters, Goal? callingGoal = null, 
			bool waitForExecution = true, long delayWhenNotWaitingInMilliseconds = 50, uint waitForXMillisecondsBeforeRunningGoal = 0, 
			int indent = 0, bool keepMemoryStackOnAsync = false, bool isolated = false)
		{

			if (goalName == null || goalName.Value == null) {
				var error = new Error($"Goal to call is empty. Calling goal is {callingGoal}");
				var output2 = new TextOutput("Error", "text/html", false, error, "desktop");
				return (engine, error, output2);
			}
			Goal? goal = null;
			ServiceContainer? container = null;
			if (goalName.Value.StartsWith("/"))
			{
				relativeAppPath = "/";
			} else
			{
				relativeAppPath = callingGoal?.RelativeGoalFolderPath ?? relativeAppPath;
			}

			string absolutePathToGoal = fileSystem.Path.Join(fileSystem.RootDirectory, relativeAppPath).AdjustPathToOs();
			string goalToRun = fileSystem.Path.Join(relativeAppPath, goalName);
			if (goalToRun.StartsWith("//")) goalToRun = goalToRun.Substring(1);

			if (isolated)
			{
				container = serviceContainerFactory.CreateContainer(context, fileSystem.RootDirectory, "/", outputStreamFactory, outputSystemStreamFactory,
					errorHandlerFactory, errorSystemHandlerFactory, askUserHandlerFactory);
				var fileAccessHandler = container.GetInstance<PLang.SafeFileSystem.IFileAccessHandler>();
				fileAccessHandler.GiveAccess(fileSystem.RootDirectory, fileSystem.OsDirectory);

				var ms = engine.GetMemoryStack();

				engine = container.GetInstance<IEngine>();
				engine.Init(container);

				
				foreach (var item in ms.GetMemoryStack())
				{
					engine.GetMemoryStack().Put(item.Value);
				}
				foreach (var item in context)
				{
					engine.GetContext().AddOrReplace(item.Key, item.Value);
				}

				goal = engine.GetGoal(goalToRun);

			} else if (CreateNewContainer(absolutePathToGoal))
			{
				var pathAndGoal = GetAppAbsolutePath(absolutePathToGoal, goalName);
				string absoluteAppStartupPath = pathAndGoal.absolutePath;
				string relativeAppStartupPath = fileSystem.Path.DirectorySeparatorChar.ToString();
				goalToRun = pathAndGoal.goalName;

				container = serviceContainerFactory.CreateContainer(context, absoluteAppStartupPath, relativeAppStartupPath, outputStreamFactory, outputSystemStreamFactory, 
					errorHandlerFactory, errorSystemHandlerFactory, askUserHandlerFactory);

				var fileAccessHandler = container.GetInstance<PLang.SafeFileSystem.IFileAccessHandler>();
				fileAccessHandler.GiveAccess(absolutePathToGoal, fileSystem.OsDirectory);

				engine = container.GetInstance<IEngine>();
				engine.Init(container);

				if (context.ContainsKey(ReservedKeywords.IsEvent))
				{
					engine.GetContext().AddOrReplace(ReservedKeywords.IsEvent, true);
				}

				goal = engine.GetGoal(goalToRun);
			} else
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
					return (engine, error2, output2);
				}

				var goals = string.Join('\n', goalsAvailable.OrderBy(p => p.GoalName).Select(p => $" - {p.GoalName} -> Path:{p.RelativeGoalPath}"));
				string strGoalsAvailable = "";
				if (!string.IsNullOrWhiteSpace(goals))
				{
					strGoalsAvailable = $" These goals are available: \n{goals}";

				}

				var error = new Error($"WARNING! - Goal '{goalName}' at {fileSystem.RootDirectory} was not found.{strGoalsAvailable}");
				var output3 = new TextOutput("Error", "text/html", false, error, "desktop");
				return (engine, error, output3);
			}

			if (waitForExecution) 
			{
				goal.ParentGoal = callingGoal;

			} else if (!keepMemoryStackOnAsync)
			{
				// todo: this does not look correct
				throw new NotImplementedException("This looks strange, need to find example of usage, otherwise remove");

				var newContext = new PLangAppContext();
				foreach (var item in context)
				{
					newContext.AddOrReplace(item.Key, item.Value);
				}
				context = newContext;
			}
			
			var memoryStack = engine.GetMemoryStack();

			if (parameters != null)
			{
				foreach (var param in parameters)
				{
					object? value = param.Value;
					if (VariableHelper.IsVariable(param.Value))
					{
						value = memoryStack.Get(param.Value?.ToString());
					}

					memoryStack.Put(param.Key.Replace("%", ""), value);
				}
			}
			var prevIndent = context.GetOrDefault(ReservedKeywords.ParentGoalIndent, 0);
			context.AddOrReplace(ReservedKeywords.ParentGoalIndent, (prevIndent + indent));
		
			var task = engine.RunGoal(goal, waitForXMillisecondsBeforeRunningGoal);
			await task.ConfigureAwait(waitForExecution);

			if (container != null)
			{
				container.Dispose();
			}
			context.AddOrReplace(ReservedKeywords.ParentGoalIndent, prevIndent);

			if (task.IsFaulted && task.Exception != null)
			{
				var error3 = new Error(task.Exception.Message, Exception: task.Exception);
				var output3 = new TextOutput("Error", "text/html", false, error3, "desktop");
				return (engine, error3, output3);
			}

			return (engine, task.Result, new TextOutput("", "text/html", false, null, "desktop"));

		}

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
