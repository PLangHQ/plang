using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Utils;
using System.Diagnostics;
using System.Reflection;
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

	// LlmRequest defaults to gpt-4.1-mini and that is what the builder has always used. Building
	// the six goal suite for real on both: gpt-5.4-mini gets every step of all six right, where
	// gpt-4.1-mini gets a render step's DontRenderMainLayout wrong on every build, and it is two
	// to three times faster besides, File 3.9s against 11.0s and AdminRoutes 6.4s against 21.4s.
	// Only these two requests are moved; the rest of the builder is untouched.
	private static string Model => Environment.GetEnvironmentVariable("PLangBatchModel") is string m && m.Length > 0
		? m : "gpt-5.4-mini";

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

	public class BatchedStepProperty
	{
		public int Number { get; set; }
		public bool WaitForExecution { get; set; } = true;
		public string? LoggerLevel { get; set; }
		public List<ErrorHandler>? ErrorHandlers { get; set; }
		public CachingHandler? CachingHandler { get; set; }
	}

	public class BatchedStepProperties
	{
		public List<BatchedStepProperty>? Steps { get; set; }
		public string? Description { get; set; }
		public Dictionary<string, string>? IncomingVariablesRequired { get; set; }
	}

	private record Planned(GoalStep Step, string Module, MethodDescription Method, ClassDescription Owner);

	// The rules from system/modules/StepPropertiesSystem.llm, which is rendered once per step with
	// the built function in it. Here they are stated once for the whole goal and the step's own
	// words decide. Measured across 73 steps of six goals, exactly one had a property that was not
	// the default, so this is a request per step to learn "nothing" 72 times.
	private const string PropertyRules = @"For each step, say how it is run, read from the step's own words.
Almost every step has none: WaitForExecution true, LoggerLevel null, ErrorHandlers null, CachingHandler null. Return that unless the step says otherwise. Never invent one.

ErrorHandlers: from an `on error ...` clause, and from any wording that says a failure must not stop the step. `dont throw error on not found` on a file step is one, and it is a handler keyed to the failure it names: {""IgnoreError"": true, ""Key"": ""FileNotFound""}. Write the handler even when the method also has a parameter that looks like it covers the same thing. The parameter is how the method behaves; the handler is how the step is run, and the step asked for both.
 - IgnoreError is true only when the step says to ignore or continue, false otherwise
 - Key is ""*"" only when the step names no particular failure, e.g. `on error call HandleError`. When it names one, the Key is that failure
 - A status code the step names goes in StatusCode as a number, with Key null: `on error status code 503` => StatusCode 503, Key null. Never write the number into Key, and never Key ""*"" together with a StatusCode
 - Every `on error` clause in a step is one handler. A step with an `on error` clause never has ErrorHandlers null
 - StatusCode, Message and Type are null unless the step states them
 - GoalToCall is the goal named after `call`, with any parameters written after its name
 - RetryHandler only when the step asks to retry; RunRetryBeforeCallingGoalToCall is true only when the retry is written before the call
Examples:
 `on error call HandleError` => [{""IgnoreError"": false, ""Key"": ""*"", ""GoalToCall"": {""Name"": ""HandleError""}}]
 `on error call Recover, ignore error` => [{""IgnoreError"": true, ""Key"": ""*"", ""GoalToCall"": {""Name"": ""Recover""}}]
 `on error key: Timeout, retry 3 times` => [{""IgnoreError"": false, ""Key"": ""Timeout"", ""RetryHandler"": {""RetryCount"": 3}}]
 `on error status code = 404, call NotFound` => [{""IgnoreError"": false, ""StatusCode"": 404, ""Key"": null, ""GoalToCall"": {""Name"": ""NotFound""}}]

WaitForExecution: false only when the step says not to wait, e.g. `dont wait`.
CachingHandler: only when the step asks for caching, e.g. `cache for 10 minutes`.
LoggerLevel: null, or one of trace|debug|info|warning|error when the step names one.
A note in parentheses after a step says what that step's method does not permit; obey it.";

	// The goal description question, from GoalBuilder.CreateDescriptionForGoal. It is asked of the
	// same material the properties are asked of, the goal's steps, so it rides in the same request.
	private const string DescriptionRules = @"Also describe the goal as a whole, in the same answer.
Description: what this goal does, read from its steps, concise, a few sentences. A goal works like a function: it defines one or more steps, and a step that writes into a variable creates it. Describe the conditions that depend on a variable and what value sends it down which path.
IncomingVariablesRequired: the variables that must be handed to this goal for it to work, as {""%name%"": ""what it is""}. Name each one exactly as the step writes it, so %request.body.q% and not %request%. A variable a step creates is not one of them, and neither is a setting such as %Settings.ApiKey%. Empty when it needs none.
Escape a %variable% written inside the Description text as \%variable\%. That escaping is for the Description only: the keys of IncomingVariablesRequired are written plainly, %name%, never escaped.";

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

		// The properties and the goal's description are read off the step's own words and need no
		// method information, so they go out beside the parameters rather than one request per
		// step. Measured by building the six goal suite for real, with this on and with it off: the
		// same steps came back right either way, so it is one llm call for a goal instead of one
		// per step for nothing.
		var propertiesTask = PrefetchProperties(goal, planned, cached);

		var results = await Task.WhenAll(batches.Select(async batch =>
		{
			var request = BuildRequest(goal, batch);
			var (result, queryError) = await llmServiceFactory.CreateHandler().Query(request, typeof(BatchedStepFunctions));
			return (Batch: batch, Request: request, Result: result, Error: queryError);
		}));
		await propertiesTask;
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
			// The type is taken from the method, never from the answer. The method information is
			// sent with short names, System.String as String and the conditions as
			// ConditionEvaluator+CompoundCondition, and the model echoed the short name back into
			// Parameter.Type, which validation then rejected as not the correct type. The declared
			// type is known here, so there is no reason to read it back at all.
			var parameters = found.Entry.Parameters?
				.Select(p => new Parameter(DeclaredType(plan.Method, p.Name) ?? p.Type, p.Name,
										   RepairLiteral(p.Value, plan.Step.Text))).ToList();
			var function = new GenericFunction("", plan.Method.MethodName, parameters, found.Entry.ReturnValues);
			cached.SetFunction(plan.Step, function, found.Request);
			filled++;
		}

		logger.Value.LogInformation($"Built the parameters of {filled} steps of {goal.GoalName} in one request");
	}

	// The second request: how every step is run, and the goal's own description, in one answer.
	// Small, because it carries no method information, only the steps.
	private async Task PrefetchProperties(Goal goal, List<Planned> planned, GoalDeciderAnswers cached)
	{
		var text = new StringBuilder();
		text.AppendLine(PropertyRules);
		text.AppendLine();
		text.AppendLine(DescriptionRules);
		text.AppendLine();
		text.AppendLine($"There are exactly {planned.Count} steps below, numbered "
			+ string.Join(", ", planned.Select(p => p.Step.Index))
			+ $". Return exactly {planned.Count} entries, using those numbers.");
		text.AppendLine();
		text.AppendLine("Answer with json: {\"Steps\": [{\"Number\": <n>, \"WaitForExecution\": true, \"LoggerLevel\": null, \"ErrorHandlers\": null, \"CachingHandler\": null}], \"Description\": \"...\", \"IncomingVariablesRequired\": {\"%name%\": \"...\"}}");
		text.AppendLine();
		foreach (var plan in planned)
		{
			text.AppendLine($"step {plan.Step.Index}:");
			text.AppendLine(plan.Step.Text.Trim());
			// What the method allows. These come off the method, which is decided before either
			// request goes out, so asking for them here costs nothing and narrows the answer.
			var allowed = Allowed(plan);
			if (allowed != null) text.AppendLine(allowed);
			text.AppendLine();
		}

		var request = new LlmRequest("BatchedStepProperties", new List<LlmMessage>
		{
			new("system", SystemText()),
			new("user", text.ToString()),
		});
		request.Goal = goal;
		request.Step = planned[0].Step;
		request.scheme = TypeHelper.GetJsonSchema(typeof(BatchedStepProperties));
		request.maxLength = Math.Clamp(planned.Count * TokensPerStep, MinimumTokens, MaximumTokens);
		// PLangBatchNoCache asks again instead of reading the llm cache, so the same goal can be
		// built repeatedly to see how stable an answer is. Measuring accuracy off a cached answer
		// measures nothing: every run returns the same characters.
		if (Environment.GetEnvironmentVariable("PLangBatchNoCache") == "1") request.Reload = true;
		request.model = Model;
		request.top_p = 0;
		request.temperature = 0;
		request.frequencyPenalty = 0;
		request.presencePenalty = 0;

		var (result, error) = await llmServiceFactory.CreateHandler().Query(request, typeof(BatchedStepProperties));
		if (error != null || result is not BatchedStepProperties answer)
		{
			logger.Value.LogWarning($"Could not build the properties of {goal.GoalName} in one request, each step will ask on its own: {error?.Message}");
			return;
		}

		var byNumber = new Dictionary<int, BatchedStepProperty>();
		foreach (var entry in answer.Steps ?? new()) byNumber[entry.Number] = entry;

		foreach (var plan in planned)
		{
			if (!byNumber.TryGetValue(plan.Step.Index, out var entry)) continue;
			cached.SetProperties(plan.Step, new Model.StepProperties("", entry.WaitForExecution,
				entry.LoggerLevel, entry.ErrorHandlers, entry.CachingHandler));
		}

		if (!string.IsNullOrEmpty(answer.Description))
		{
			// The keys are written plainly. Told to escape a %variable% inside the description, the
			// model escaped the dictionary keys too and one came back as "\0request.body.q\0c".
			var incoming = answer.IncomingVariablesRequired?
				.ToDictionary(p => "%" + p.Key.Trim('%', '\\', '\0') + "%", p => p.Value);
			cached.SetDescription(goal, goal.GetGoalAsString(), answer.Description, incoming);
		}

		logger.Value.LogDebug($"Built the properties of {cached.PropertyCount} steps of {goal.GoalName}, and its description, in one request");
	}

	// Only the parts a method actually permits. A method marked [MethodSettings] can forbid
	// caching, error handling or running without being waited for, and saying so is what the per
	// step prompt did by rendering the template with those three flags.
	private string? Allowed(Planned plan)
	{
		var programType = typeHelper.GetRuntimeType(plan.Module);
		var method = programType?.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
			.FirstOrDefault(m => m.Name == plan.Method.MethodName);
		var settings = method?.GetCustomAttribute<Attributes.MethodSettingsAttribute>();
		if (settings == null) return null;

		var forbidden = new List<string>();
		if (!settings.CanBeCached) forbidden.Add("CachingHandler is always null");
		if (!settings.CanHaveErrorHandling) forbidden.Add("ErrorHandlers is always null");
		if (!settings.CanBeAsync) forbidden.Add("WaitForExecution is always true");
		return forbidden.Count == 0 ? null : "  (" + string.Join(", ", forbidden) + ")";
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
		// PLangBatchNoCache asks again instead of reading the llm cache, so the same goal can be
		// built repeatedly to see how stable an answer is. Measuring accuracy off a cached answer
		// measures nothing: every run returns the same characters.
		if (Environment.GetEnvironmentVariable("PLangBatchNoCache") == "1") request.Reload = true;
		request.model = Model;
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
			// A blank line between blocks, so one method's examples do not read as part of the
			// next method's parameters.
			text.AppendLine(RenderMethod(plan.Module, plan.Method));
			text.AppendLine();
		}

		var reachable = Reachable(planned, types);
		if (reachable.Count > 0)
		{
			text.AppendLine("## types ##");
			foreach (var type in reachable)
			{
				text.AppendLine(RenderType(type));
				text.AppendLine();
			}
		}
		return text.ToString();
	}

	private static string RenderMethod(string module, MethodDescription method)
	{
		var text = new StringBuilder($"{Short(module)}.{method.MethodName}");
		// The description is written exactly as it stands, line breaks and all, and not indented.
		// Collapsed onto one line, the five worked examples in RenderTemplate's description ran
		// together; indented four spaces to hang off the method name, they read as one block with
		// the parameters and a step targeting #main came back with DontRenderMainLayout true in 4
		// builds out of 4. A description is laid out by whoever wrote it; leave the layout alone.
		if (!string.IsNullOrEmpty(method.Description)) text.Append("  // " + Normalise(method.Description));
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
			// A parameter whose type is a record carries a pointer to the types block appended to
			// its own description. Skipping any description that mentions SupportingObjects threw
			// the description away with the pointer, so every complex parameter was sent with
			// nothing said about it: QuerySqlFile's parameters came back as the bare variable
			// rather than a list of one entry, and the query then binds nothing. Drop the pointer,
			// which the types block below makes redundant, and keep what was written.
			var description = OneLine(parameter.Description ?? "");
			var pointer = description.IndexOf("(see ", StringComparison.Ordinal);
			if (pointer >= 0 && description.Contains("SupportingObjects")) description = description[..pointer].Trim();
			if (description.Length > 0) text.Append($"  // {description}");
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
		// One comment line per line of the description. RenderMessage's runs to six lines, one for
		// Content, Target, Actions, Level, Channel and Actor; run together into a single comment it
		// is a wall of text, and a step targeting #main came back with DontRenderMainLayout true.
		if (!string.IsNullOrEmpty(type.Description))
		{
			foreach (var line in Normalise(type.Description).Split('\n'))
			{
				if (line.Trim().Length > 0) text.Append("\n    // " + line.Trim());
			}
		}
		text.AppendLine();
		foreach (var property in type.TypeProperties ?? new())
		{
			text.Append($"    {property.Name}: {Short(property.Type)}");
			if (!property.IsRequired) text.Append(" optional");
			if (property is EnumDescription e && !string.IsNullOrEmpty(e.AvailableValues)) text.Append($"  // one of: {e.AvailableValues}");
			if (!string.IsNullOrEmpty(property.Description)) text.Append($"  // {Normalise(property.Description)}");
			text.AppendLine();
		}
		return text.ToString().TrimEnd();
	}

	private string UserText(Goal goal, List<Planned> planned)
	{
		var text = new StringBuilder();

		// The per step scheme, the one the system prompt's rules are written against, goes first.
		// Without it a render step stopped setting IsTemplateFile in 5 builds out of 5, although
		// the parameter and its description were both in the request: the rules refer to a scheme
		// the request never showed.
		text.AppendLine("Every step's answer must follow this scheme:");
		text.AppendLine(TypeHelper.GetJsonSchema(typeof(GenericFunction)));
		text.AppendLine();

		// The answer shape has to be written out here, not left to the scheme alone. Without it the
		// model answered with a flat object of parameter names per step, in order, with no step
		// number on any of them, and every step but the first was dropped on the way back.
		text.AppendLine("The module and the method of every step below are already decided. Do not choose them again. For each step return only its parameters and its return values.");
		text.AppendLine();
		text.AppendLine("Every Name must be copied from the chosen method's own parameter list, spelled exactly as it is written there. A method has no parameters other than the ones listed for it. Never invent a name, never translate one into a more natural word, and put a value the method has no parameter for into ReturnValues or leave it out.");
		text.AppendLine();
		// One request carrying more than one job started flattening a record parameter into dotted
		// entries, goalInfo.Name and goalInfo.Parameters, which binds nothing.
		text.AppendLine("A parameter whose type is an object or a record is ONE entry whose Value is that whole object, e.g. {\"Name\": \"goalInfo\", \"Value\": {\"Name\": \"/auth/SignIn\"}}. Never split it into one entry per property and never write a dotted name such as goalInfo.Name.");
		text.AppendLine();

		// Number the steps you send, do not describe a range. Saying "numbered 0 to n-1" while
		// labelling a step `step 5` made the model answer 0, and the answer then belongs to no step.
		var numbers = string.Join(", ", planned.Select(p => p.Step.Index));
		text.AppendLine($"There are exactly {planned.Count} steps below, numbered {numbers}. Return exactly {planned.Count} entries, using those numbers.");
		text.AppendLine();

		// Last before the steps. Placement is not cosmetic here: this line at the top of the
		// message instead of next to the steps left a render step answering with the wrong
		// DontRenderMainLayout in 3 builds out of 3.
		text.AppendLine("Answer with json: {\"Steps\": [{\"Number\": <the step number>, \"Parameters\": [{\"Type\": \"<c# type>\", \"Name\": \"<parameter name>\", \"Value\": <the value>}], \"ReturnValues\": [{\"Type\": \"<c# type>\", \"VariableName\": \"<variable name without percent signs>\"}]}]}");
		text.AppendLine("ReturnValues is null when the step writes its result into no variable.");
		text.AppendLine();

		foreach (var plan in planned)
		{
			// The module's full name, as the method blocks above are keyed, and nothing else. A line
			// listing the step's variables and their types was tried here and is not sent: it was
			// never in what was measured, and every line added to a step is a line competing with
			// the step's own words.
			text.AppendLine($"step {plan.Step.Index}: calls {plan.Module}.{plan.Method.MethodName}");
			text.AppendLine(plan.Step.Text.Trim());
			text.AppendLine();
		}
		return text.ToString();
	}

	// A quoted text is copied out of the step, so it IS in the step. When a returned string is not,
	// but something very like it is, the model retyped it instead of copying it. Measured on one
	// Icelandic goal it dropped a letter from `raunveruleg`, lowercased `Prófið`, and three times
	// swapped a word for the same word in another script: `þú` came back as Bengali `তুমি`,
	// `aðeins` as Georgian `მხოლოდ`, once Pashto. Sharper wording in the prompt took that from 3
	// builds in 12 to 1 in 12 and no further, so the fix is to put the step's own characters back.
	//
	// This reads no meaning and assumes no syntax: it asks only whether the answer is nearly, but
	// not exactly, something the step already says, so it works whatever language the step is
	// written in. A value that is not close to anything in the step is left alone, which is every
	// ordinary answer: a %variable%, a number, a file name, a value the step never spelled out.
	private const double RepairThreshold = 0.90;
	private const int ShortestRepairable = 12;

	internal static object? RepairLiteral(object? value, string stepText)
	{
		if (value is Newtonsoft.Json.Linq.JValue jv && jv.Type == Newtonsoft.Json.Linq.JTokenType.String)
		{
			return RepairLiteral(jv.ToString(), stepText);
		}
		if (value is not string text) return value;
		if (text.Length < ShortestRepairable || stepText.Contains(text, StringComparison.Ordinal)) return value;

		string? best = null;
		double bestRatio = 0;
		// The width is tried either side of the answer's own length: a retyped literal can be
		// shorter or longer than the original, and fixing it to the answer's length cut the
		// trailing full stop off the repair.
		for (int width = Math.Max(8, text.Length - 6); width <= text.Length + 6; width++)
		{
			if (width > stepText.Length) break;
			for (int start = 0; start + width <= stepText.Length; start++)
			{
				var window = stepText.Substring(start, width);
				var ratio = Similarity(text, window);
				if (ratio > bestRatio) (best, bestRatio) = (window, ratio);
			}
		}
		return bestRatio >= RepairThreshold && best != null ? best : value;
	}

	// Length of the longest common subsequence against the mean length, the same measure python's
	// difflib reports, so the threshold means what it meant when it was measured.
	private static double Similarity(string a, string b)
	{
		if (a.Length == 0 || b.Length == 0) return 0;
		var previous = new int[b.Length + 1];
		var current = new int[b.Length + 1];
		for (int i = 1; i <= a.Length; i++)
		{
			for (int j = 1; j <= b.Length; j++)
			{
				current[j] = a[i - 1] == b[j - 1] ? previous[j - 1] + 1 : Math.Max(previous[j], current[j - 1]);
			}
			(previous, current) = (current, previous);
			Array.Clear(current);
		}
		return 2.0 * previous[b.Length] / (a.Length + b.Length);
	}

	private static string? DeclaredType(MethodDescription method, string parameterName)
		=> (method.Parameters ?? new()).FirstOrDefault(p => p.Name == parameterName)?.Type;

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

	private static string Normalise(string text)
		=> text.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
}
