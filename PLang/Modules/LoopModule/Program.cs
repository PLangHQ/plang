

using Microsoft.Extensions.Logging;
using PLang.Attributes;
using PLang.Runtime;
using System.Collections;
using System.ComponentModel;

namespace PLang.Modules.LoopModule
{
	[Description("While, for, foreach, loops through a list")]
	public class Program : BaseProgram
	{
		private readonly ILogger logger;
		private readonly IPseudoRuntime pseudoRuntime;
		private readonly IEngine engine;

		public Program(ILogger logger, IPseudoRuntime pseudoRuntime, IEngine engine) : base()
		{
			this.logger = logger;
			this.pseudoRuntime = pseudoRuntime;
			this.engine = engine;
		}

		public async Task RunLoop([HandlesVariableAttribute] string variableToLoopThrough, string goalNameToCall, [HandlesVariableAttribute] Dictionary<string, object>? parameters = null)
		{
			if (parameters == null) parameters = new();

			string listName = parameters.ContainsKey("list") ? parameters["list"].ToString().Replace("%", "") : "list";
			string listCountName = parameters.ContainsKey("listCount") ? parameters["listCount"].ToString().Replace("%", "") : "listCount";
			string itemName = parameters.ContainsKey("item") ? parameters["item"].ToString().Replace("%", "") : "item";
			string positionName = parameters.ContainsKey("position") ? parameters["position"].ToString().Replace("%", "") : "position";

			var obj = memoryStack.Get(variableToLoopThrough);
			if (obj is IList list)
			{
				if (list == null || list.Count == 0)
				{
					logger.LogWarning($"{variableToLoopThrough} is an empty list. Nothing to loop through");
					return;
				}

				for (int i = 0; i < list.Count; i++)
				{
					var goalParameters = new Dictionary<string, object>();
					goalParameters.Add(listName.ToString()!, list);
					goalParameters.Add(listCountName, list.Count);
					goalParameters.Add(itemName.ToString()!, list[i]);
					goalParameters.Add(positionName.ToString()!, i+1);

					var missingEntries = parameters.Where(p => !goalParameters.ContainsKey(p.Key));
					foreach (var entry in missingEntries)
					{
						goalParameters.Add(entry.Key, entry.Value);
					}

					await pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath, goalNameToCall, goalParameters, Goal);
				}
			} else if (obj is IEnumerable enumerables)
			{
				int idx = 1;
				foreach (var item in enumerables)
				{
					var goalParameters = new Dictionary<string, object>();
					goalParameters.Add(listName.ToString()!, enumerables);
					goalParameters.Add(itemName.ToString()!, item);
					goalParameters.Add(positionName.ToString()!, idx++);
					goalParameters.Add(listCountName, -1);
					var missingEntries = parameters.Where(p => !goalParameters.ContainsKey(p.Key));
					foreach (var entry in missingEntries)
					{
						goalParameters.Add(entry.Key, entry.Value);
					}

					await pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath, goalNameToCall, goalParameters, Goal);
				}
			}
			


		}


	}
}

