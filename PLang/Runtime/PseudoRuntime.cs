using LightInject;
using Nethereum.ABI.Util;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Utils;

namespace PLang.Runtime
{
	public interface IPseudoRuntime
	{
		Task RunGoal(IEngine engine, PLangAppContext context, string appPath, string goalName, Dictionary<string, object> parameters, Goal? callingGoal = null);
	}

	public class PseudoRuntime : IPseudoRuntime
	{
		private readonly PrParser prParser;
		private readonly IServiceContainerFactory serviceContainerFactory;
		private readonly IPLangFileSystem fileSystem;
		private readonly IOutputStream outputStream;

		public PseudoRuntime(PrParser prParser, IServiceContainerFactory serviceContainerFactory, IPLangFileSystem fileSystem, IOutputStream outputStream)
		{
			this.prParser = prParser;
			this.serviceContainerFactory = serviceContainerFactory;
			this.fileSystem = fileSystem;
			this.outputStream = outputStream;
		}

		public async Task RunGoal(IEngine engine, PLangAppContext context, string appPath, string goalName, Dictionary<string, object> parameters, Goal? callingGoal = null)
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
				container = serviceContainerFactory.CreateContainer(context, goal.AbsoluteAppStartupFolderPath, goal.RelativeGoalFolderPath, outputStream);

				engine = container.GetInstance<IEngine>();
				engine.Init(container);

				if (context.ContainsKey(ReservedKeywords.IsEvent))
				{
					engine.AddContext(ReservedKeywords.IsEvent, true);
				}
			}
			
			var memoryStack = engine.GetMemoryStack();
			if (parameters != null)
			{
				foreach (var param in parameters)
				{
					memoryStack.Put(param.Key.Replace("%", ""), param.Value);
				}
			}

			await engine.RunGoal(goal);

			if (container != null)
			{
				container.Dispose();
			}
		}

	}


}
