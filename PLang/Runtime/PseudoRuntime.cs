using LightInject;
using Nethereum.ABI.Util;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Services.AppsRepository;
using PLang.Services.OutputStream;
using PLang.Utils;
using PLang.Errors;
using PLang.Errors.Handlers;

namespace PLang.Runtime
{
    public interface IPseudoRuntime
	{
		Task<(IEngine engine, IError? error)> RunGoal(IEngine engine, PLangAppContext context, string appPath, string goalName, Dictionary<string, object?>? parameters, Goal? callingGoal = null, bool waitForExecution = true, long delayWhenNotWaitingInMilliseconds = 50);
	}

	public class PseudoRuntime : IPseudoRuntime
	{
		private readonly IServiceContainerFactory serviceContainerFactory;
		private readonly IPLangFileSystem fileSystem;
		private readonly IOutputStreamFactory outputStreamFactory;
		private readonly IOutputSystemStreamFactory outputSystemStreamFactory;
		private readonly IErrorHandlerFactory exceptionHandlerFactory;
		private readonly IAskUserHandlerFactory askUserHandlerFactory;
		
		public PseudoRuntime(IServiceContainerFactory serviceContainerFactory, IPLangFileSystem fileSystem,
			IOutputStreamFactory outputStreamFactory, IOutputSystemStreamFactory outputSystemStreamFactory, IErrorHandlerFactory exceptionHandlerFactory, IAskUserHandlerFactory askUserHandlerFactory)
		{
			this.serviceContainerFactory = serviceContainerFactory;
			this.fileSystem = fileSystem;
			this.outputStreamFactory = outputStreamFactory;
			this.outputSystemStreamFactory = outputSystemStreamFactory;
			this.exceptionHandlerFactory = exceptionHandlerFactory;
			this.askUserHandlerFactory = askUserHandlerFactory;
		}

		public async Task<(IEngine engine, IError? error)> RunGoal(IEngine engine, PLangAppContext context, string appPath, string goalName, Dictionary<string, object?>? parameters, Goal? callingGoal = null, bool waitForExecution = true, long delayWhenNotWaitingInMilliseconds = 50)
		{

			Goal? goal = null;
			ServiceContainer? container = null;
			goalName = goalName.Replace("!", "");

			string absolutePathToGoal = Path.Join(fileSystem.RootDirectory, appPath, goalName).AdjustPathToOs();
			string goalToRun = goalName;
			if (CreateNewContainer(absolutePathToGoal))
			{
				var pathAndGoal = GetAppAbsolutePath(absolutePathToGoal);
				string absoluteAppStartupPath = pathAndGoal.absolutePath;
				string relativeAppStartupPath = Path.DirectorySeparatorChar.ToString();
				goalToRun = pathAndGoal.goalName;

				container = serviceContainerFactory.CreateContainer(context, absoluteAppStartupPath, relativeAppStartupPath, outputStreamFactory, outputSystemStreamFactory, exceptionHandlerFactory, askUserHandlerFactory);

				engine = container.GetInstance<IEngine>();
				engine.Init(container);

				if (context.ContainsKey(ReservedKeywords.IsEvent))
				{
					engine.AddContext(ReservedKeywords.IsEvent, true);
				}

				goal = engine.GetGoal(goalToRun);
			} else
			{
				goal = engine.GetGoal(goalToRun, callingGoal);
			}
			

			if (goal == null)
			{
				var goalsAvailable = engine.GetGoalsAvailable(appPath, goalToRun);
				var goals = string.Join('\n', goalsAvailable.Select(p => $" - {p.GoalName}"));
				string strGoalsAvailable = "";
				if (!string.IsNullOrWhiteSpace(goals))
				{
					strGoalsAvailable = $" These goals are available: \n{goals}";

				}
				return (engine, new Error($"WARNING! - Goal '{goalName}' at {fileSystem.RootDirectory} was not found.{strGoalsAvailable}"));
			}
			if (waitForExecution) 
			{
				goal.ParentGoal = callingGoal;

			} else
			{
				var newContext = new PLangAppContext();
				foreach (var item in context)
				{
					newContext.Add(item.Key, item.Value);
				}
				engine.GetContext().Clear();
				engine.GetContext().AddOrReplace(newContext);
			}

			var memoryStack = engine.GetMemoryStack();
			if (parameters != null)
			{
				foreach (var param in parameters)
				{
					memoryStack.Put(param.Key.Replace("%", ""), param.Value);
				}
			}

			var task = engine.RunGoal(goal);
			

			if (waitForExecution)
			{
				try
				{
					await task;
				}
				catch { }
			} else if (delayWhenNotWaitingInMilliseconds > 0)
			{
				await Task.Delay((int) delayWhenNotWaitingInMilliseconds);
			}
			
			if (container != null)
			{
				container.Dispose();
			}

			if (task.IsFaulted && task.Exception != null)
			{
				var error = new Error(task.Exception.Message, Exception: task.Exception);
				return (engine, error);
			}
			return (engine, task.Result);

		}

		public (string absolutePath, string goalName) GetAppAbsolutePath(string absolutePathToGoal)
		{
			absolutePathToGoal = absolutePathToGoal.AdjustPathToOs();

			Dictionary<string, int> dict = new Dictionary<string, int>();
			dict.Add("apps", absolutePathToGoal.LastIndexOf("apps"));
			dict.Add(".modules", absolutePathToGoal.LastIndexOf(".modules"));
			dict.Add(".services", absolutePathToGoal.LastIndexOf(".services"));

			var item = dict.OrderByDescending(p => p.Value).FirstOrDefault();
			if (item.Value == -1) return (absolutePathToGoal, "");

			var idx = absolutePathToGoal.IndexOf(Path.DirectorySeparatorChar, item.Value + item.Key.Length + 1);
			if (idx == -1)
			{
				idx = absolutePathToGoal.IndexOf(Path.DirectorySeparatorChar, item.Value + item.Key.Length);
			}

			var absolutePathToApp = absolutePathToGoal.Substring(0, idx);
			foreach (var itemInDict in dict)
			{
				if (absolutePathToApp.EndsWith(itemInDict.Key))
				{
					absolutePathToApp = absolutePathToGoal;
				}
			}
			if (absolutePathToApp.EndsWith(Path.DirectorySeparatorChar))
			{
				absolutePathToApp = absolutePathToApp.TrimEnd(Path.DirectorySeparatorChar);
			}

			var goalName = absolutePathToGoal.Replace(absolutePathToApp, "").TrimStart(Path.DirectorySeparatorChar);
			if (string.IsNullOrEmpty(goalName)) goalName = "Start";

			return (absolutePathToApp, goalName);
		}

		private bool CreateNewContainer(string absoluteGoalPath)
		{
			string servicesFolder = Path.Join(fileSystem.RootDirectory, ".services");
			string modulesFolder = Path.Join(fileSystem.RootDirectory, ".modules");
			string appsFolder = Path.Join(fileSystem.RootDirectory, "apps");
			return absoluteGoalPath.StartsWith(servicesFolder) || absoluteGoalPath.StartsWith(modulesFolder) || absoluteGoalPath.StartsWith(appsFolder);
		}




	}


}
