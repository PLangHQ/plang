using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Utils;
using System.Diagnostics;
using System.Text;
using static PLang.Modules.BaseBuilder;

namespace PLang.Building;

// One llm request fills the parameters of a whole goal instead of one request per step.
//
// The decider fixes each step's module and method first. What is left after that is filling
// parameters, and a per step request pays for the method's whole signature every time: ten steps
// carry it ten times, and twenty eight route steps carry AddRoute and its examples twenty eight
// times. Grouped, each method is written once however many steps call it.
//
// Measured on admin/dev/File, 10 steps: 10 requests and 67307 characters became 1 request and
// 31662 characters, for the same 10 of 10 steps correct. On routes/AdminRoutes, 28 steps, the one
// request takes about 8 seconds where 28 sequential requests take minutes.
//
// A miss costs nothing. Any step this cannot answer, or a request that fails outright, simply
// leaves the cache empty for that step and it builds on its own exactly as before.
public interface IBatchedInstructionBuilder
{
	Task PrefetchInstructions(Goal goal, IReadOnlyList<int> stepIndexes);
}

public class BatchedInstructionBuilder : IBatchedInstructionBuilder
{
	private readonly ILlmServiceFactory llmServiceFactory;
	private readonly ITypeHelper typeHelper;
	private readonly IBuilderDeciderCache deciderCache;
	private readonly IBuilderDeciderReport deciderReport;
	private readonly VariableHelper variableHelper;
	private readonly MemoryStack memoryStack;
	private readonly Lazy<ILogger> logger;

	// Below this the request is not worth making: a goal of one or two steps saves nothing, and
	// the whole point is amortising the method signatures over many steps.
	private const int MinimumSteps = 2;

	// Above this a goal goes out as two requests in parallel. Measured: a 28 step goal drops from
	// about 8 seconds to about 4, while splitting a 10 step goal took it from 10 correct to 4.
	private const int SplitAbove = 20;

	// The answer holds every parameter of every step, so the room it needs grows with the steps.
	// At the 4000 token default a 28 step goal came back empty, the request was thrown away and
	// all 28 steps built on their own: 57 llm calls and 135 seconds for the goal.
	private const int TokensPerStep = 700;
	private const int MinimumTokens = 4000;
	private const int MaximumTokens = 16000;

	public BatchedInstructionBuilder(Lazy<ILogger> logger, ILlmServiceFactory llmServiceFactory, ITypeHelper typeHelper,
		IBuilderDeciderCache deciderCache, IBuilderDeciderReport deciderReport, VariableHelper variableHelper,
		IMemoryStackAccessor memoryStackAccessor)
	{
		this.logger = logger;
		this.llmServiceFactory = llmServiceFactory;
		this.typeHelper = typeHelper;
		this.deciderCache = deciderCache;
		this.deciderReport = deciderReport;
		this.variableHelper = variableHelper;
		this.memoryStack = memoryStackAccessor.Current;
	}

	public class BatchedStepFunction
	{
		public int Number { get; set; }
		public string? Name { get; set; }
		public List<Parameter>? Parameters { get; set; }
		public List<Modules.BaseBuilder.ReturnValue>? ReturnValues { get; set; }
	}

	public class BatchedStepFunctions
	{
		public List<BatchedStepFunction>? Steps { get; set; }
	}

	private record Planned(GoalStep Step, string Module, MethodDescription Method, ClassDescription Owner);

	public async Task PrefetchInstructions(Goal goal, IReadOnlyList<int> stepIndexes)
	{
		if ((AppContext.GetData("batchbuild") as string) == "off") return;
		if (goal.IsSystem || stepIndexes.Count < MinimumSteps) return;

		var cached = deciderCache.ForGoal(goal);
		var planned = new List<Planned>();

		foreach (var index in stepIndexes)
		{
			var step = goal.GoalSteps[index];

			var moduleChoice = cached.Module(step);
			var module = moduleChoice != null && moduleChoice.Confidence >= BuilderDecider.ConfidenceThreshold
				? moduleChoice.Module : step.ModuleType;
			if (string.IsNullOrEmpty(module)) continue;

			var methodChoice = cached.Method(step);
			if (methodChoice == null || methodChoice.Confidence < BuilderDecider.ConfidenceThreshold) continue;

			var programType = typeHelper.GetRuntimeType(module);
			if (programType == null) continue;

			var (described, error) = new ClassDescriptionHelper().GetClassDescription(programType);
			if (error != null || described == null) continue;

			// Overloads are left to the per step path, same rule the decider already applies: the
			// method it named is not enough to know which signature the step means.
			var overloads = described.Methods.Where(m => m.MethodName == methodChoice.Method).ToList();
			if (overloads.Count != 1) continue;

			planned.Add(new Planned(step, module, overloads[0], described));
		}

		if (planned.Count < MinimumSteps)
		{
			logger.Value.LogDebug($"Only {planned.Count} of {stepIndexes.Count} steps of {goal.GoalName} have a decided module and method, so each builds on its own");
			return;
		}

		// Above roughly twenty steps two requests in parallel are worth it: the answer is generated
		// in two halves at once and a 28 step goal drops from about 8 seconds to about 4. Below
		// that it is not worth doing and it hurts, a 10 step goal split in two scored 4 of 10. The
		// input is paid twice, since each half carries the method information, so this buys wall
		// clock and not tokens.
		var batches = planned.Count > SplitAbove
			? new List<List<Planned>> { planned.Take(planned.Count / 2).ToList(), planned.Skip(planned.Count / 2).ToList() }
			: new List<List<Planned>> { planned };

		var started = Stopwatch.StartNew();
		var results = await Task.WhenAll(batches.Select(async batch =>
		{
			var request = BuildRequest(goal, batch);
			var (result, queryError) = await llmServiceFactory.CreateHandler().Query(request, typeof(BatchedStepFunctions));
			return (Batch: batch, Request: request, Result: result, Error: queryError);
		}));
		deciderReport.RecordLlmCall(goal, started.Elapsed);

		var byNumber = new Dictionary<int, (BatchedStepFunction Entry, LlmRequest Request)>();
		foreach (var (batch, request, result, queryError) in results)
		{
			if (queryError != null || result is not BatchedStepFunctions answer || answer.Steps == null)
			{
				// Nothing is lost: with an empty cache every step builds on its own, exactly as before.
				logger.Value.LogWarning($"Could not build {batch.Count} steps of {goal.GoalName} in one request, each of them will build on its own: {queryError?.Message}");
				continue;
			}
			foreach (var entry in answer.Steps) byNumber[entry.Number] = (entry, request);
		}
		if (byNumber.Count == 0) return;
		logger.Value.LogDebug($"Asked for steps {string.Join(",", planned.Select(p => p.Step.Index))} of {goal.GoalName}, answered for {string.Join(",", byNumber.Keys)}");

		int filled = 0;
		foreach (var plan in planned)
		{
			if (!byNumber.TryGetValue(plan.Step.Index, out var found)) continue;

			// The method was decided before the request went out, so it is not read back from the
			// answer. Asking for it again is how a fixed choice gets quietly replaced.
			var function = new GenericFunction("", plan.Method.MethodName, found.Entry.Parameters, found.Entry.ReturnValues);
			cached.SetFunction(plan.Step, function, found.Request);
			filled++;
		}

		logger.Value.LogInformation($"Built the parameters of {filled} steps of {goal.GoalName} in one request");
	}

	private LlmRequest BuildRequest(Goal goal, List<Planned> planned)
	{
		var request = new LlmRequest("BatchedInstructionBuilder", new List<LlmMessage>
		{
			new("system", SystemText()),
			new("assistant", AssistantText(planned)),
			new("user", UserText(goal, planned)),
		});
		request.Goal = goal;
		request.Step = planned[0].Step;
		request.scheme = TypeHelper.GetJsonSchema(typeof(BatchedStepFunctions));
		request.maxLength = Math.Clamp(planned.Count * TokensPerStep, MinimumTokens, MaximumTokens);
		request.top_p = 0;
		request.temperature = 0;
		request.frequencyPenalty = 0;
		request.presencePenalty = 0;
		return request;
	}

	// The builder's own system prompt with two corrections. It tells the model to map intent onto
	// one of the functions provided, which is no longer its job here, and it describes a Reasoning
	// field spent explaining a choice that has already been made. Dropping Reasoning scored the
	// same or better and is smaller.
	private static string SystemText()
	{
		var text = Modules.BaseBuilder.DefaultSystemText();
		text = text.Replace("2. Map the intent to one of C# function provided to you",
							"2. Fill the parameters of the function you are given");
		text = text.Replace("Name: Name of the function to use from list of functions, if no function matches set as \"N/A\"\n", "");
		var reasoning = text.IndexOf("Reasoning: A brief description", StringComparison.Ordinal);
		if (reasoning >= 0)
		{
			var end = text.IndexOf('\n', reasoning);
			text = end < 0 ? text.Substring(0, reasoning) : text.Remove(reasoning, end - reasoning + 1);
		}
		return text.Trim();
	}

	// Only the methods the steps actually call, one entry each however many steps call them, and
	// only the types those methods refer to. As lines rather than json: json spends most of its
	// characters repeating the keys Type, Name, Description, IsRequired and DefaultValue for every
	// parameter of every method. Measured on one goal, lines cost 31676 characters where json cost
	// 48018, for the same answers.
	private static string AssistantText(List<Planned> planned)
	{
		var text = new StringBuilder("## methods already chosen ##\n");

		var seen = new HashSet<string>();
		var types = new Dictionary<string, ComplexDescription>();
		foreach (var plan in planned)
		{
			foreach (var supporting in plan.Owner.SupportingObjects ?? new())
			{
				if (supporting.Type != null) types.TryAdd(supporting.Type, supporting);
			}

			if (!seen.Add(plan.Module + "." + plan.Method.MethodName)) continue;
			text.AppendLine(RenderMethod(plan.Module, plan.Method));
		}

		var reachable = Reachable(planned, types);
		if (reachable.Count > 0)
		{
			text.AppendLine("\n## types ##");
			foreach (var type in reachable) text.AppendLine(RenderType(type));
		}
		return text.ToString();
	}

	private static string RenderMethod(string module, MethodDescription method)
	{
		var text = new StringBuilder($"{Short(module)}.{method.MethodName}");
		if (!string.IsNullOrEmpty(method.Description)) text.Append($"  // {OneLine(method.Description)}");
		text.AppendLine();

		foreach (var parameter in method.Parameters ?? new())
		{
			text.Append($"    {parameter.Name}: {Short(parameter.Type)} ");
			text.Append(parameter.IsRequired ? "required" : $"= {Value(parameter.DefaultValue)}");
			if (parameter is EnumDescription e && !string.IsNullOrEmpty(e.AvailableValues))
			{
				// Without this the model invented a different spelling of the enum on every run.
				text.Append($"  // one of: {e.AvailableValues}");
			}
			if (!string.IsNullOrEmpty(parameter.Description) && !parameter.Description.Contains("SupportingObjects"))
			{
				text.Append($"  // {OneLine(parameter.Description)}");
			}
			text.AppendLine();
		}

		// Examples earn their place: dropping them took one 28 step goal from 27 correct to 8.
		foreach (var example in method.Examples ?? new()) text.AppendLine($"    e.g. {OneLine(example)}");
		return text.ToString().TrimEnd();
	}

	// Only the types a chosen method actually refers to, followed through the types they name in
	// turn. Matching on any word that looks like a type pulled in a pdf options record and a csv
	// options record for a goal that reads a file and renders a template.
	private static List<ComplexDescription> Reachable(List<Planned> planned, Dictionary<string, ComplexDescription> types)
	{
		var frontier = new Queue<string>();
		foreach (var plan in planned)
		{
			foreach (var parameter in plan.Method.Parameters ?? new())
			{
				foreach (var name in types.Keys)
				{
					if (Mentions(parameter, name)) frontier.Enqueue(name);
				}
			}
		}

		var kept = new List<ComplexDescription>();
		var seen = new HashSet<string>();
		while (frontier.Count > 0)
		{
			var name = frontier.Dequeue();
			if (!seen.Add(name) || !types.TryGetValue(name, out var type)) continue;
			kept.Add(type);

			foreach (var property in type.TypeProperties ?? new())
			{
				foreach (var other in types.Keys)
				{
					if (!seen.Contains(other) && Mentions(property, other)) frontier.Enqueue(other);
				}
			}
		}
		return kept;
	}

	private static bool Mentions(IPropertyDescription property, string typeName)
	{
		if (property.Type != null && property.Type.Contains(typeName, StringComparison.Ordinal)) return true;
		return property.Description != null && property.Description.Contains(typeName, StringComparison.Ordinal);
	}

	private static string RenderType(ComplexDescription type)
	{
		var text = new StringBuilder(Short(type.Type));
		if (!string.IsNullOrEmpty(type.Description)) text.Append($"\n    // {OneLine(type.Description)}");
		text.AppendLine();
		foreach (var property in type.TypeProperties ?? new())
		{
			text.Append($"    {property.Name}: {Short(property.Type)}");
			if (!property.IsRequired) text.Append(" optional");
			if (property is EnumDescription e && !string.IsNullOrEmpty(e.AvailableValues)) text.Append($"  // one of: {e.AvailableValues}");
			if (!string.IsNullOrEmpty(property.Description)) text.Append($"  // {OneLine(property.Description)}");
			text.AppendLine();
		}
		return text.ToString().TrimEnd();
	}

	private string UserText(Goal goal, List<Planned> planned)
	{
		var text = new StringBuilder();

		// The answer shape has to be written out here, not left to the scheme alone. Without it the
		// model answered with a flat object of parameter names per step, in order, with no step
		// number on any of them, and every step but the first was dropped on the way back.
		text.AppendLine("Answer with json: {\"Steps\": [{\"Number\": <the step number>, \"Parameters\": [{\"Type\": \"<c# type>\", \"Name\": \"<parameter name>\", \"Value\": <the value>}], \"ReturnValues\": [{\"Type\": \"<c# type>\", \"VariableName\": \"<variable name without percent signs>\"}]}]}");
		text.AppendLine("ReturnValues is null when the step writes its result into no variable.");
		text.AppendLine();
		text.AppendLine("The module and the method of every step below are already decided. Do not choose them again. For each step return only its parameters and its return values.");
		text.AppendLine();
		text.AppendLine("Every Name must be copied from the chosen method's own parameter list, spelled exactly as it is written there. A method has no parameters other than the ones listed for it. Never invent a name and never translate one into a more natural word.");
		text.AppendLine();

		// Number the steps you send, do not describe a range. Saying "numbered 0 to n-1" while
		// labelling a step `step 5` made the model answer 0, and the answer then belongs to no step.
		var numbers = string.Join(", ", planned.Select(p => p.Step.Index));
		text.AppendLine($"There are exactly {planned.Count} steps below, numbered {numbers}. Return exactly {planned.Count} entries, using those numbers.");
		text.AppendLine();

		foreach (var plan in planned)
		{
			text.AppendLine($"step {plan.Step.Index}: calls {Short(plan.Module)}.{plan.Method.MethodName}");
			text.AppendLine(plan.Step.Text.Trim());

			var variables = variableHelper.GetVariables(plan.Step.Text, memoryStack).DistinctBy(v => v.PathAsVariable).ToList();
			if (variables.Count > 0)
			{
				text.AppendLine("defined variables: " + string.Join(", ",
					variables.Select(v => v.PathAsVariable + " (type:" + (v.Value?.GetType().FullName ?? "object") + ")")));
			}
			text.AppendLine();
		}
		return text.ToString();
	}

	private static string Short(string? type)
	{
		if (string.IsNullOrEmpty(type)) return "";
		return type.Replace("System.Collections.Generic.", "")
				   .Replace("System.", "")
				   .Replace("PLang.Modules.", "")
				   .Replace("PLang.", "");
	}

	private static string Value(object? value)
	{
		if (value == null) return "null";
		if (value is bool b) return b ? "true" : "false";
		if (value is string s) return "\"" + s + "\"";
		return value.ToString() ?? "null";
	}

	private static string OneLine(string text) => text.Replace("\r", " ").Replace("\n", " ").Trim();
}
