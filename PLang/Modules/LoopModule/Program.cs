

using IdGen;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Utilities;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Models;
using PLang.Modules.DbModule;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Utils;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Dynamic.Core;
using static PLang.Modules.ConditionalModule.ConditionEvaluator;

namespace PLang.Modules.LoopModule
{
	[Description("While, for, foreach, loops, repeat, go through a list and call a goal")]
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


		public IEnumerable<IReadOnlyList<T>> Split<T>(IEnumerable<T> source, int size)
		{
			if (source is null) throw new ArgumentNullException(nameof(source));
			if (size <= 0)
			{
				foreach (var x in source) yield return new[] { x };
				yield break;
			}
			foreach (var chunk in source.Chunk(size)) yield return chunk;
		}

		[Description("Repeat a call to a function number of time. Great for pagination, where repeatCount is page count and startIndex is page number. startIndex starts at 0")]
		public async Task<IError?> Repeat(int repeatCounter, [HandlesVariableAttribute] GoalToCallInfo goalToCall, int startIndex = 0)
		{
			var groupedErrors = new GroupedErrors("RepeatErrors");
			for (int i = startIndex; i < repeatCounter; i++)
			{
				(var returnEngine, var variables, var error) = await pseudoRuntime.RunGoal(engine, contextAccessor, goal.RelativeAppStartupFolderPath, goalToCall, goal);
				if (error != null) return error;
			}
			return null;
		}

		public IEnumerable<IReadOnlyList<object>> Split(IEnumerable source, int size) =>
			Split(source.Cast<object>(), size);

		public record MultiThreaded(int ThreadCount = 1, double CpuUsage = 0, bool FailFast = true, [HandlesVariable] GoalToCallInfo? GoalToCallBeforeItemIsProcessed = null, [HandlesVariable] GoalToCallInfo? GoalToCallAfterItemIsProcessed = null);
		public record LinqOptions(int Split = 0);

		[Description(@"Predefined variables are %list%, %item%, %position%, %listCount%, user can overwrite those using parameters, e.g. `- go through %products%, call goal ProcessProduct item=%product%`, parameter key is ""item"" and value is ""%product%"". cpuUsage is percentage of cpu cores available, 80% => 0.8")]
		
		public async Task<IError?> RunLoop([HandlesVariableAttribute] string variableToLoopThrough, [HandlesVariableAttribute] GoalToCallInfo goalToCall,
		 MultiThreaded? multiThreaded = null, LinqOptions? linqOptions = null)
		{
			var parameters = goalToCall.Parameters;
			if (parameters == null) parameters = new();

			string listName = GetParameterName(parameters, "list");
			string itemName = GetParameterName(parameters, "item");
			string positionName = GetParameterName(parameters, "position");

			var prevItem = memoryStack.Get("item");
			var prevList = memoryStack.Get("list");
			var prevPosition = memoryStack.Get("position");

			int effectiveThreads = 1;

			if (multiThreaded == null) multiThreaded = new();

			var groupedErrors = new GroupedErrors("LoopErrors");
			if (multiThreaded.CpuUsage > 0 || multiThreaded.ThreadCount > 1)
			{
				int cores = Environment.ProcessorCount;
				effectiveThreads = multiThreaded.ThreadCount > 1 ? multiThreaded.ThreadCount : (int)(cores * multiThreaded.CpuUsage);
				effectiveThreads = Math.Max(effectiveThreads, 1);
			}



			var obj = memoryStack.Get(variableToLoopThrough);
			if (obj == null)
			{
				logger.LogDebug($"{variableToLoopThrough} does not exist. Have you created it? Check for spelling error", goalStep, function);
				return null;
			}
			if (obj is ObjectValue ov2)
			{
				obj = ov2.Value;
			}

			if (obj is string || obj.GetType().IsPrimitive)
			{
				var l = new List<object>();
				l.Add(obj);
				obj = l;
			}

			if (obj.GetType().Name.StartsWith("KeyValuePair`2"))
			{
				obj = obj.GetType().GetProperty("Value").GetValue(obj);
			}

			if (obj is JToken jtoken && !jtoken.HasValues)
			{
				return null;
			}

			if (obj is not IEnumerable enumerables)
			{
				return new ProgramError($"{variableToLoopThrough} is not list of items");
			}

			bool hasEntry = enumerables.AsQueryable().Any();
			if (!hasEntry && (obj is JValue || obj is JObject))
			{
				var list = new List<object>();
				list.Add(obj);
				enumerables = list;
				hasEntry = true;
			}

			if (linqOptions != null && linqOptions.Split > 0)
			{
				enumerables = Split(enumerables, linqOptions.Split);
			}

			int idx = 0;
			if (effectiveThreads == 1)
			{
				var nonDefaultParameters = goalToCall.Parameters.Where(p => !p.Key.Equals("item", StringComparison.OrdinalIgnoreCase) &&
						!p.Key.Equals("list", StringComparison.OrdinalIgnoreCase) &&
						!p.Key.Equals("position", StringComparison.OrdinalIgnoreCase));
				foreach (var param in nonDefaultParameters)
				{
					goalToCall.Parameters.AddOrReplace(param.Key, memoryStack.LoadVariables(param.Value));
				}
				var items = enumerables.ToDynamicList();
				for (int i = 0;i<items.Count;i++) 
				{
					goalToCall.Parameters.AddOrReplace(listName.ToString()!, items);
					var item = items[i] as object;

					if (item is ObjectValue ov)
					{
						goalToCall.Parameters.AddOrReplace(itemName.ToString()!, ov.Value);
					}
					else
					{
						goalToCall.Parameters.AddOrReplace(itemName.ToString()!, item);
					}
					goalToCall.Parameters.AddOrReplace(positionName.ToString()!, idx++);

					var result = await pseudoRuntime.RunGoal(engine, contextAccessor, goal.RelativeAppStartupFolderPath, goalToCall, Goal);
					if (result.Error != null && result.Error is not IErrorHandled) return result.Error;

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
						goalToCall.Parameters.AddOrReplace(listName.ToString()!, enumerables);
						if (item is ObjectValue ov)
						{
							goalToCall.Parameters.AddOrReplace(itemName.ToString()!, ov.Value);
						}
						else
						{
							goalToCall.Parameters.AddOrReplace(itemName.ToString()!, item);
						}
						goalToCall.Parameters.AddOrReplace(positionName.ToString()!, idx++);

						Task<(IEngine engine, object? Variables, IError? error)> task;
						if (multiThreaded.GoalToCallBeforeItemIsProcessed != null)
						{
							multiThreaded.GoalToCallBeforeItemIsProcessed.Parameters.AddOrReplaceDict(goalToCall.Parameters);
							task = pseudoRuntime.RunGoal(engine, contextAccessor, goal.RelativeAppStartupFolderPath, multiThreaded.GoalToCallBeforeItemIsProcessed, Goal, isolated: true);
							await task;
						}

						task = pseudoRuntime.RunGoal(engine, contextAccessor, goal.RelativeAppStartupFolderPath, goalToCall, Goal, isolated: true);
						var result = await task;
						if (result.error != null)
						{
							if (multiThreaded.FailFast)
							{
								cts?.Cancel();
								return result.error;
							}
							else
							{
								groupedErrors.Add(result.error);
							}
						}

						if (multiThreaded.GoalToCallAfterItemIsProcessed != null)
						{
							multiThreaded.GoalToCallAfterItemIsProcessed.Parameters.AddOrReplaceDict(goalToCall.Parameters);
							task = pseudoRuntime.RunGoal(engine, contextAccessor, goal.RelativeAppStartupFolderPath, multiThreaded.GoalToCallAfterItemIsProcessed, Goal, isolated: true);
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



			memoryStack.Put("item", prevItem);
			memoryStack.Put("list", prevList);
			memoryStack.Put("position", prevPosition);

			if (groupedErrors.Count > 0)
			{
				return groupedErrors;
			}

			return null;

		}


	}
}

