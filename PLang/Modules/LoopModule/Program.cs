using System.Collections;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Errors;
using PLang.Models;
using PLang.Runtime;

namespace PLang.Modules.LoopModule;

[Description("While, for, foreach, loops through a list")]
public class Program : BaseProgram
{
    private readonly IEngine engine;
    private readonly ILogger logger;
    private readonly IPseudoRuntime pseudoRuntime;

    public Program(ILogger logger, IPseudoRuntime pseudoRuntime, IEngine engine)
    {
        this.logger = logger;
        this.pseudoRuntime = pseudoRuntime;
        this.engine = engine;
    }

    private string GetParameterName(Dictionary<string, object?>? parameters, string name)
    {
        if (parameters == null) return name;

        if (parameters.ContainsKey(name)) return parameters[name].ToString().Replace("%", "");

        var valueAsKey =
            parameters.FirstOrDefault(p => p.Value.ToString().Equals(name, StringComparison.OrdinalIgnoreCase));
        if (valueAsKey.Key != null) return valueAsKey.Key.Replace("%", "");
        return name;
    }

    [Description(
        "Call another Goal, when ! is prefixed, e.g. !RenameFile or !Google/Search, parameters are sent to the goal being called")]
    public async Task<IError?> RunLoop([HandlesVariableAttribute] string variableToLoopThrough,
        GoalToCall goalNameToCall, [HandlesVariableAttribute] Dictionary<string, object?>? parameters = null)
    {
        if (parameters == null) parameters = new Dictionary<string, object>();

        var listName = GetParameterName(parameters, "list");
        var listCountName = GetParameterName(parameters, "listCount");
        var itemName = GetParameterName(parameters, "item");
        var positionName = GetParameterName(parameters, "position");

        var prevItem = memoryStack.Get("item");
        var prevList = memoryStack.Get("list");
        var prevListCount = memoryStack.Get("listCount");
        var prevPosition = memoryStack.Get("position");


        var obj = memoryStack.Get(variableToLoopThrough);
        if (obj == null)
        {
            logger.LogDebug($"{variableToLoopThrough} does not exist. Have you created it? Check for spelling error",
                goalStep, function);
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

            for (var i = 0; i < list.Count; i++)
            {
                var goalParameters = new Dictionary<string, object?>();
                goalParameters.Add(listName!, list);
                goalParameters.Add(listCountName, list.Count);
                goalParameters.Add(itemName!, list[i]);
                goalParameters.Add(positionName!, i);

                var missingEntries = parameters.Where(p => !goalParameters.ContainsKey(p.Key.Replace("%", "")));
                foreach (var entry in missingEntries) goalParameters.Add(entry.Key, entry.Value);

                var result = await pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath,
                    goalNameToCall, goalParameters, Goal);
                if (result.error != null) return result.error;
            }
        }
        else if (obj is IEnumerable enumerables)
        {
            var idx = 1;
            var hasEntry = false;
            foreach (var item in enumerables)
            {
                hasEntry = true;
                var goalParameters = new Dictionary<string, object?>();
                goalParameters.Add(listName!, enumerables);
                goalParameters.Add(itemName!, item);
                goalParameters.Add(positionName!, idx++);
                goalParameters.Add(listCountName, -1);
                var missingEntries = parameters.Where(p => !goalParameters.ContainsKey(p.Key));
                foreach (var entry in missingEntries) goalParameters.Add(entry.Key, entry.Value);

                var result = await pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath,
                    goalNameToCall, goalParameters, Goal);
                if (result.error != null) return result.error;
            }

            if (!hasEntry && (obj is JValue || obj is JObject))
            {
                List<object> objs = new();
                var goalParameters = new Dictionary<string, object?>();
                goalParameters.Add(listName!, objs);
                goalParameters.Add(itemName!, obj);
                goalParameters.Add(positionName!, 0);
                goalParameters.Add(listCountName, -1);
                var missingEntries = parameters.Where(p => !goalParameters.ContainsKey(p.Key));
                foreach (var entry in missingEntries) goalParameters.Add(entry.Key, entry.Value);

                var result = await pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath,
                    goalNameToCall, goalParameters, Goal);
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