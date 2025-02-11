

using IdGen;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Models;
using PLang.Runtime;
using System.Collections;
using System.ComponentModel;

namespace PLang.Modules.LoopModule
{
	[Description("While, for, foreach, loops, go through a list")]
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

		private string GetParameterName(Dictionary<string, object?>? parameters, string name)
		{
			if (parameters == null) return name;

			if (parameters.ContainsKey(name))
			{
				return parameters[name].ToString().Replace("%", "");
			}

			var valueAsKey = parameters.FirstOrDefault(p => p.Value.ToString().Equals(name, StringComparison.OrdinalIgnoreCase));
			if (valueAsKey.Key != null) return valueAsKey.Key.Replace("%", "");
			return name;
		}

		[Description("Call another Goal, when ! is prefixed, e.g. !RenameFile or !Google/Search, parameters are sent to the goal being called. Predefined variables are %list%, %item%, %position%, %listCount%, use can overwrite those using parameters")]
		public async Task<IError?> RunLoop([HandlesVariableAttribute] string variableToLoopThrough, GoalToCall goalNameToCall, [HandlesVariableAttribute] Dictionary<string, object?>? parameters = null)
		{
			if (parameters == null) parameters = new();

			string listName = GetParameterName(parameters, "list");
			string listCountName = GetParameterName(parameters, "listCount");
			string itemName = GetParameterName(parameters, "item");
			string positionName = GetParameterName(parameters, "position");

			var prevItem = memoryStack.Get("item");
			var prevList = memoryStack.Get("list");
			var prevListCount = memoryStack.Get("listCount");
			var prevPosition = memoryStack.Get("position");


			var obj = memoryStack.Get(variableToLoopThrough);
			if (obj == null)
			{
				logger.LogDebug($"{variableToLoopThrough} does not exist. Have you created it? Check for spelling error", goalStep, function);
				return null;
			}
			if (obj is string || obj.GetType().IsPrimitive)
			{
				var l = new List<object>();
				l.Add(obj);
				obj = l;
			}

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
					goalParameters.Add(positionName.ToString()!, i);

					var missingEntries = parameters.Where(p => !goalParameters.ContainsKey(p.Key.Replace("%", "")));
					foreach (var entry in missingEntries)
					{
						goalParameters.Add(entry.Key, entry.Value);
					}

					var result = await pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath, goalNameToCall, goalParameters, Goal);
					if (result.error != null) return result.error;
				}

			}
			else if (obj is JToken jtoken && !jtoken.HasValues) { }
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

