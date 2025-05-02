

using IdGen;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Utilities;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Dynamic.Core;

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

		[Description("Call another Goal, when ! is prefixed, e.g. !RenameFile or !Google/Search, parameters are sent to the goal being called. Predefined variables are %list%, %item%, %position%, %listCount%, use can overwrite those using parameters. cpuUsage is percentage of cpu cores available, 80% => 0.8")]
		public async Task<IError?> RunLoop([HandlesVariableAttribute] string variableToLoopThrough, GoalToCall goalNameToCall, [HandlesVariableAttribute] Dictionary<string, object?>? parameters = null,
			 int threadCount = 1, double cpuUsage = 0, bool failFast = true, [HandlesVariable] string? goalToCallBeforeItemIsProcessed = null, [HandlesVariable] string? goalToCallAfterItemIsProcessed = null)
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

			int effectiveThreads = 1;

			var groupedErrors = new GroupedErrors("LoopErrors");
			if (cpuUsage > 0 || threadCount > 1)
			{
				int cores = Environment.ProcessorCount;
				effectiveThreads = threadCount > 1 ? threadCount : (int)(cores * cpuUsage);
				effectiveThreads = Math.Max(effectiveThreads, 1);
			}



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

			if (obj is JToken jtoken && !jtoken.HasValues)
			{
				return null;
			}
			else if (obj is IEnumerable enumerables)
			{
				bool hasEntry = enumerables.AsQueryable().Any();
				if (!hasEntry && (obj is JValue || obj is JObject))
				{
					var list = new List<object>();
					list.Add(obj);
					enumerables = list;
					hasEntry = true;
				}

				int idx = 1;
				if (effectiveThreads == 1)
				{
					foreach (var item in enumerables)
					{
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
				}
				else
				{

					using var semaphore = new SemaphoreSlim(effectiveThreads);
					using var cts = new CancellationTokenSource();

					CancellationToken token = cts?.Token ?? CancellationToken.None;

					var tasks = enumerables.Cast<object>().Select(async item =>
					{
						await semaphore.WaitAsync(token);
						try
						{
							
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

							Task<(IEngine engine, object? Variables, IError? error, IOutput? output)> task;
							if (goalToCallBeforeItemIsProcessed != null)
							{
								task = pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath, goalToCallBeforeItemIsProcessed, goalParameters, Goal);
								await task;
							}

							task = pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath, goalNameToCall, goalParameters, Goal);
							var result = await task;
							if (result.error != null)
							{
								if (failFast)
								{
									cts?.Cancel();
									return result.error;
								} else
								{
									groupedErrors.Add(result.error);
								}
							}

							if (goalToCallAfterItemIsProcessed != null)
							{
								task = pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath, goalToCallAfterItemIsProcessed, goalParameters, Goal);
								await task;
							}

						}
						finally
						{
							semaphore.Release();
						}
						return null;
					});

					try
					{
						var results = await Task.WhenAll(tasks);
						var result = results.FirstOrDefault(e => e != null);
						return result;
					}
					catch (OperationCanceledException)
					{
						var result = tasks.FirstOrDefault(t => t.IsCompletedSuccessfully && t.Result != null)?.Result;
						return result;
					}
				}
			}
			

			memoryStack.Put("item", prevItem);
			memoryStack.Put("list", prevList);
			memoryStack.Put("listCount", prevListCount);
			memoryStack.Put("position", prevPosition);

			if (groupedErrors.Count > 0)
			{
				return groupedErrors;
			}

			return null;

		}


	}
}

