using LightInject;
using Nethereum.ABI.Util;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Services.OutputStream;
using PLang.Utils;

namespace PLang.Runtime
{
    public interface IPseudoRuntime
	{
		Task RunGoal(IEngine engine, PLangAppContext context, string appPath, string goalName, Dictionary<string, object?>? parameters, Goal? callingGoal = null, bool waitForExecution = true, long delayWhenNotWaitingInMilliseconds = 50);
	}

	public class PseudoRuntime : IPseudoRuntime
	{
		private readonly PrParser prParser;
		private readonly IServiceContainerFactory serviceContainerFactory;
		private readonly IPLangFileSystem fileSystem;

		public PseudoRuntime(PrParser prParser, IServiceContainerFactory serviceContainerFactory, IPLangFileSystem fileSystem)
		{
			this.prParser = prParser;
			this.serviceContainerFactory = serviceContainerFactory;
			this.fileSystem = fileSystem;
		}

		public async Task RunGoal(IEngine engine, PLangAppContext context, string appPath, string goalName, Dictionary<string, object?>? parameters, Goal? callingGoal = null, bool waitForExecution = true, long delayWhenNotWaitingInMilliseconds = 50)
		{
			Goal? goal = prParser.GetGoalByAppAndGoalName(appPath, goalName, callingGoal);

			if (goal == null)
			{
				var goalsAvailable = prParser.GetGoalsAvailable(appPath, goalName);
				var goals = string.Join('\n', goalsAvailable.Select(p => $" - {p.GoalName}"));
				//throw new Exception($"Goal {goalName} couldn't be found. Did you type in correct name?");
				throw new GoalNotFoundException($"WARNING! - Goal '{goalName}' was not found. These goals are available: \n{goals} ", appPath, goalName);
			}

			ServiceContainer? container = null;
			if (!goal.AbsoluteGoalPath.StartsWith(fileSystem.RootDirectory))
			{
				container = serviceContainerFactory.CreateContainer(context, goal.AbsoluteAppStartupFolderPath, goal.RelativeGoalFolderPath, engine.OutputStream);

				engine = container.GetInstance<IEngine>();
				engine.Init(container);

				if (context.ContainsKey(ReservedKeywords.IsEvent))
				{
					engine.AddContext(ReservedKeywords.IsEvent, true);
				}
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
				await task;
			} else if (delayWhenNotWaitingInMilliseconds > 0)
			{
				await Task.Delay((int) delayWhenNotWaitingInMilliseconds);
			}
			
			if (container != null)
			{
				container.Dispose();
			}

			if (task.Exception != null)
			{
				throw task.Exception;
			}

		}

	}


}
