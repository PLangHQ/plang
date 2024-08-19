

using IdGen;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Errors;
using PLang.Models;
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

		[Description("Call another Goal, when ! is prefixed, e.g. !RenameFile or !Google/Search, parameters are sent to the goal being called")]
		public async Task<IError?> RunLoop([HandlesVariableAttribute] string variableToLoopThrough, GoalToCall goalNameToCall, [HandlesVariableAttribute] Dictionary<string, object>? parameters = null)
		{
			if (parameters == null) parameters = new();

			string listName = parameters.ContainsKey("list") ? parameters["list"].ToString().Replace("%", "") : "list";
			string listCountName = parameters.ContainsKey("listCount") ? parameters["listCount"].ToString().Replace("%", "") : "listCount";
			string itemName = parameters.ContainsKey("item") ? parameters["item"].ToString().Replace("%", "") : "item";
			string positionName = parameters.ContainsKey("position") ? parameters["position"].ToString().Replace("%", "") : "position";

			var prevItem = memoryStack.Get("item");
			var prevList = memoryStack.Get("list");
			var prevListCount = memoryStack.Get("listCount");
			var prevPosition = memoryStack.Get("position");


			var obj = memoryStack.Get(variableToLoopThrough);
			if (obj is IList list)
			{
				if (list == null || list.Count == 0)
				{
					logger.LogDebug($"{variableToLoopThrough} is an empty list. Nothing to loop through");
					return null;
				}

				for (int i = 0; i < list.Count; i++)
				{
					var goalParameters = new Dictionary<string, object?>();
					goalParameters.Add(listName.ToString()!, list);
					goalParameters.Add(listCountName, list.Count);
					goalParameters.Add(itemName.ToString()!, list[i]);
					goalParameters.Add(positionName.ToString()!, i + 1);

					var missingEntries = parameters.Where(p => !goalParameters.ContainsKey(p.Key));
					foreach (var entry in missingEntries)
					{
						goalParameters.Add(entry.Key, entry.Value);
					}

					var result = await pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath, goalNameToCall, goalParameters, Goal);
					if (result.error != null) return result.error;
				}
			}
			else if (obj is IEnumerable enumerables)
			{
				int idx = 1;
				bool hasEntry = false;
				foreach (var item in enumerables)
				{
					hasEntry = true;
					var goalParameters = new Dictionary<string, object?>();
					goalParameters.Add(listName.ToString()!, enumerables);
					goalParameters.Add(itemName.ToString()!, item);
					goalParameters.Add(positionName.ToString()!, idx++);
					goalParameters.Add(listCountName, -1);
					var missingEntries = parameters.Where(p => !goalParameters.ContainsKey(p.Key));
					foreach (var entry in missingEntries)
					{
						goalParameters.Add(entry.Key, entry.Value);
					}

					var result = await pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath, goalNameToCall, goalParameters, Goal);
					if (result.error != null) return result.error;
				}

				if (!hasEntry && (obj is JValue || obj is JObject))
				{
					List<object> objs = new();
					var goalParameters = new Dictionary<string, object?>();
					goalParameters.Add(listName.ToString()!, objs);
					goalParameters.Add(itemName.ToString()!, obj);
					goalParameters.Add(positionName.ToString()!, 0);
					goalParameters.Add(listCountName, -1);
					var missingEntries = parameters.Where(p => !goalParameters.ContainsKey(p.Key));
					foreach (var entry in missingEntries)
					{
						goalParameters.Add(entry.Key, entry.Value);
					}

					var result = await pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath, goalNameToCall, goalParameters, Goal);
					if (result.error != null) return result.error;
				}
			} 


			memoryStack.Put("item", prevItem);
			memoryStack.Put("list", prevList);
			memoryStack.Put("listCount", prevListCount);
			memoryStack.Put("position", prevPosition);

			return null;

		}


	}
}

