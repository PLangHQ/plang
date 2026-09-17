using Jil;
using LightInject;
using Microsoft.Extensions.Logging;
using MimeKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Building;
using PLang.Building.Model;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Utils;
using PLang.Utils.Extractors;
using PLang.Utils.JsonConverters;
using System;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Instruction = PLang.Building.Model.Instruction;

namespace PLang.Modules
{

	[GreatLeapAttribute]  // or [Class]
	public abstract class BaseBuilder : IBaseBuilder
	{

		private string? system;
		private string? assistant;
		private List<string> appendedSystemCommand;
		private List<string> appendedAssistantCommand;
		private string module;
		private IPLangFileSystem fileSystem;
		private ILlmServiceFactory llmServiceFactory;
		private ITypeHelper typeHelper;
		private ILogger logger;
		protected MemoryStack memoryStack;
		protected PLangContext context;
		private VariableHelper variableHelper;
		private IContentExtractor contentExtractor;
		private PLang.Building.IBuilderDecider? decider;
		protected GoalStep GoalStep;


#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		protected BaseBuilder()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		{ }



		[Init]
		public void InitBaseBuilder(GoalStep goalStep, IPLangFileSystem fileSystem, ILlmServiceFactory llmServiceFactory, ITypeHelper typeHelper,
			MemoryStack memoryStack, PLangContext context, VariableHelper variableHelper, ILogger logger, PLang.Building.IBuilderDecider? decider = null)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			this.decider = decider;
			logger.LogDebug($"        - Start InitBaseBuilder - {stopwatch.ElapsedMilliseconds}");
			this.GoalStep = goalStep;
			this.module = goalStep.ModuleType;
			this.fileSystem = fileSystem;
			this.llmServiceFactory = llmServiceFactory;
			this.typeHelper = typeHelper;
			this.memoryStack = memoryStack;
			this.context = context;
			this.variableHelper = variableHelper;
			this.logger = logger;


			appendedSystemCommand = new List<string>();
			appendedAssistantCommand = new List<string>();

			logger.LogDebug($"        - End InitBaseBuilder - {stopwatch.ElapsedMilliseconds}");
		}

		public void SetStep(GoalStep step)
		{
			this.GoalStep = step;
		}

		public void SetContentExtractor(IContentExtractor contentExtractor)
		{
			this.contentExtractor = contentExtractor;
		}
		protected string GetPath(string? path, Goal goal)
		{
			return PathHelper.GetPath(path, fileSystem, goal);
		}

		public async Task<(T?, IBuilderError? Error)> LlmRequest<T>(string system, GoalStep step)
		{
			List<LlmMessage> messages = new();

			messages.Add(new LlmMessage("system", system));
			messages.Add(new LlmMessage("user", step.Text));
			if (step.ValidationErrors.Count > 0)
			{
				var builderError = new BuilderError("");
				builderError.ErrorChain.AddRange(step.ValidationErrors);

				messages.Add(new LlmMessage("assistant", ErrorHelper.MakeForLlm(builderError)));
			}
			LlmRequest llmRequest = new LlmRequest(typeof(T).FullName, messages);
			llmRequest.Step = step;
			llmRequest.Goal = step.Goal;

			(var result, var queryError) = await llmServiceFactory.CreateHandler().Query(llmRequest, typeof(T));
			if (queryError != null)
			{
				return ((T?)result, new BuilderError(queryError));
			}
			return ((T?)result, null);
		}


		public virtual async Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build<T>(GoalStep step,
			IBuilderError? previousBuildError = null)
		{
			return await Build(step, typeof(T), previousBuildError);
		}
		public virtual async Task<(Instruction? Instruction, IBuilderError? BuilderError)> BuildWithClassDescription<T>(GoalStep step, ClassDescription classDescription,
					IBuilderError? previousBuildError = null)
		{
			return await BuildInternal(step, typeof(T), previousBuildError, classDescription);
		}
		public virtual async Task<(Instruction? Instruction, IBuilderError? BuilderError)> BuildWithClassDescription(GoalStep step, ClassDescription classDescription,
			IBuilderError? previousBuildError = null)
		{
			return await BuildInternal(step, typeof(GenericFunction), previousBuildError, classDescription);
		}
		public virtual async Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build(GoalStep step,
			IBuilderError? previousBuildError = null)
		{
			return await Build(step, typeof(GenericFunction), previousBuildError);
		}

		[Method]
		public virtual async Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build(GoalStep step, Type responseType,
			IBuilderError? previousBuildError = null)
		{
			var result = await BuildInternal(step, responseType, previousBuildError, null);
			return result;
		}

		private async Task<(Instruction? Instruction, IBuilderError? BuilderError)> BuildInternal(GoalStep step, Type? responseType = null,
			IBuilderError? previousBuildError = null, ClassDescription? classDescription = null)
		{

			if (responseType == null) responseType = typeof(GenericFunction);

			// Phase 3 only runs on a method this builder narrowed itself, never on a description a
			// module builder supplied, so the decided one is kept apart from the caller's.
			ClassDescription? decided = null;
			if (classDescription == null)
			{
				decided = await NarrowToDecidedMethod(step, previousBuildError);
				classDescription = decided;
			}
			var question = GetLlmRequest(step, responseType, previousBuildError, classDescription);

			if (decided != null)
			{
				Instruction? built = null;
				try
				{
					built = await DecideParameters(step, decided, question);
				}
				catch (Exception ex)
				{
					// A bug in the decider must never break a build: name it and let the llm fill the step.
					logger.LogWarning($"{step.LineNumber}: Decider threw {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
				}
				if (built != null)
				{
					appendedSystemCommand.Clear();
					appendedAssistantCommand.Clear();
					assistant = "";
					system = "";
					return (built, null);
				}
			}

			try
			{


				(var result, var queryError) = await llmServiceFactory.CreateHandler().Query(question, responseType);
				if (queryError != null) return (null, new BuilderError(queryError));

				if (result == null || (result is string str && string.IsNullOrEmpty(str)))
				{
					return (null, new StepBuilderError($"Could not build for {responseType.Name}", step));
				}

				var instruction = InstructionCreator.Create(result, responseType, step, question);

				

				//cleanup for next time
				appendedSystemCommand.Clear();
				appendedAssistantCommand.Clear();
				assistant = "";
				system = "";


				return (instruction, null);
			}
			catch (ParsingException ex)
			{
				string? innerMessage = ex.InnerException?.Message;
				if (ex.InnerException?.InnerException != null)
				{
					innerMessage = ex.InnerException?.InnerException.Message;
				}

				return (null, new StepBuilderError(
					$@"
<error>
{innerMessage}
{ex.Message}
<error>

Previous LLM request resulted in this error, see in <error>. 
Make sure to use the information in <error> to return valid JSON response"
, step));
			} catch	(Exception ex2)
			{
				string? innerMessage = ex2.InnerException?.Message;
				if (ex2.InnerException?.InnerException != null)
				{
					innerMessage = ex2.InnerException?.InnerException.Message;
				}
				

				return (null, new StepBuilderError(
					$@"
<error>
{innerMessage}
{ex2.Message}
<error>
<llm_response>
{question.RawResponse}
<llm_response>
"
, step, ex: ex2, Retry: false, ContinueBuild: false));
			}
		}

		[Property]
		public record Parameter(string Type, string Name, object? Value);
		[Property]
		public record ReturnValue(string Type, string VariableName);
		[Property]
		public record ComplexReturnValue(string Type, string VariableName, List<ReturnValue> Properties) : ReturnValue(Type, VariableName);
		[Property]
		public record GenericFunction(string Reasoning, string Name, List<Parameter>? Parameters = null, List<ReturnValue>? ReturnValues = null) : IGenericFunction
		{
			[LlmIgnore]
			[Newtonsoft.Json.JsonIgnore]
			public Instruction Instruction { get; set; }

			public T? GetParameter<T>(string name, T? defaultValue)
			{
				var item = GetParameter<T>(name);
				if (item == null || (item is string str && string.IsNullOrEmpty(str))) return defaultValue;
				return item;
			}

			public T? GetParameter<T>(string name)
			{
				var parameter = Parameters?.FirstOrDefault(p => p.Name == name);
				if (parameter == null) return default;

				return (T?)TypeHelper.ConvertToType(parameter.Value, typeof(T), new PlaceholderPrimitiveConverter());
			}
			public GenericFunction SetParameter(string name, object value)
			{
				if (Parameters == null) return this;

				var idx = Parameters.FindIndex(p => p.Name == name);
				if (idx == -1) return this;

				var parameter = Parameters[idx];
				Parameters[idx] = parameter with { Value = value };

				return this;
			}
		}

		public interface IGenericFunction
		{
			string Reasoning { get; }
			string Name { get; }
			List<Parameter>? Parameters { get; }
			List<ReturnValue>? ReturnValues { get; }

			[LlmIgnore]
			[Newtonsoft.Json.JsonIgnore]
			public Instruction Instruction { get; set; }
		}
		[Method]
		public void AppendToSystemCommand(string appendedSystemCommand)
		{
			this.appendedSystemCommand.Add(appendedSystemCommand);
		}
		[Method]
		public void SetSystem(string systemCommand)
		{
			this.system = systemCommand;
		}
		[Method]
		public void AppendToAssistantCommand(string appendedAssistantCommand)
		{
			this.appendedAssistantCommand.Add(appendedAssistantCommand);
		}
		[Method]
		public void SetAssistant(string assistantCommand)
		{
			this.assistant = assistantCommand;
		}
		string model = null;
		[Method]
		public void SetModel(string model)
		{
			this.model = model;
		}
		[Method]
		// The decider picks the method; the llm then only fills that method's parameters instead of
		// guessing the method as well. A caller that already narrowed the description (a module
		// builder) is left alone, and a retry goes back to the full list so the llm can correct a
		// wrong pick. All overloads of the chosen name are kept so the llm still picks the right one.
		private async Task<ClassDescription?> NarrowToDecidedMethod(GoalStep step, IBuilderError? previousBuildError)
		{
			if (decider == null || previousBuildError != null || step.Goal?.IsSystem == true || !PLang.Building.BuilderDecider.IsOn()) return null;

			var programType = typeHelper.GetRuntimeType(module);
			if (programType == null) return null;

			var (classDescription, error) = new ClassDescriptionHelper().GetClassDescription(programType);
			if (error != null || classDescription == null) return null;
			if (classDescription.Methods.Select(m => m.MethodName).Distinct().Count() <= 1) return null;

			var (choice, deciderError) = await decider.ChooseMethod(step.Text, module, MethodCriteria(classDescription));
			if (deciderError != null)
			{
				logger.LogWarning($"{step.LineNumber}: Decider could not choose a method, llm picks from all methods: {deciderError.Message}");
				return null;
			}

			var overloads = classDescription.Methods.Where(m => m.MethodName == choice?.Method).ToList();
			if (choice == null || overloads.Count == 0 || choice.Confidence < PLang.Building.BuilderDecider.ConfidenceThreshold)
			{
				logger.LogInformation($"{step.LineNumber}: Decider method {choice?.Method} ({choice?.Confidence:0.00}) not trusted, llm picks from all methods");
				return null;
			}

			logger.LogInformation($"{step.LineNumber}: Decider chose method {choice.Method} ({choice.Confidence:0.00}) for {step.Text.Trim(['\n', '\r', '\t'])}");
			return new ClassDescription
			{
				Description = classDescription.Description,
				ExampleInformation = classDescription.ExampleInformation,
				Methods = overloads,
				SupportingObjects = classDescription.SupportingObjects
			};
		}

		// The option key is the method name (what the decider hands back); the description is the
		// signature plus the method's own description, so lookalike methods can be told apart.
		private static Dictionary<string, string> MethodCriteria(ClassDescription classDescription)
		{
			var criteria = new Dictionary<string, string>();
			foreach (var method in classDescription.Methods)
			{
				if (criteria.ContainsKey(method.MethodName)) continue;
				var parameters = string.Join(", ", (method.Parameters ?? new()).Select(p => $"{p.Name}: {p.Type}"));
				var description = string.IsNullOrWhiteSpace(method.Description) ? "" : " " + method.Description;
				criteria[method.MethodName] = $"{method.MethodName}({parameters}){description}";
			}
			return criteria;
		}

		private const string NoneOption = "__none__";

		// With the method known, every parameter is a choice from a finite set the step itself
		// provides: a %variable% in scope, a quoted literal, a number, an enum name or a bool, plus
		// "none of these", which means "leave unset" for an optional parameter and "the value is
		// something else" for a required one. All questions go in one call. It is all or nothing:
		// one parameter the step cannot answer (a complex type, a string not present in the step,
		// which is what SQL and code look like, or a low-confidence pick) and the whole step goes to
		// the llm, which still only sees this one method. On success the function is built here
		// and the llm is never called for the step.
		private async Task<Instruction?> DecideParameters(GoalStep step, ClassDescription decided, LlmRequest question)
		{
			if (decider == null || !PLang.Building.BuilderDecider.IsOn()) return null;
			if (decided.Methods.Count != 1)
			{
				logger.LogInformation($"{step.LineNumber}: Decider leaves parameters to llm, {decided.Methods.Count} overloads of {decided.Methods.FirstOrDefault()?.MethodName}");
				return null;
			}

			var method = decided.Methods[0];
			// Paths come back without their %; the runtime only treats a value as a variable when it
			// is wrapped, so wrap here or a decided variable would be written as a literal string.
			var variables = variableHelper.GetVariables(step.Text, memoryStack).Select(v => "%" + v.Path.Trim('%') + "%").Distinct().ToList();
			var literals = QuotedLiterals(step.Text);
			var numbers = Numbers(step.Text);

			var questions = new Dictionary<string, Dictionary<string, string>>();
			foreach (var parameter in method.Parameters ?? new())
			{
				// An optional parameter the step never mentions is left unset so its default applies.
				// Asking about it only invites an unsure answer that would veto the whole step, and
				// leaving it out is exactly what the llm does with it.
				if (!parameter.IsRequired && step.Text.IndexOf(parameter.Name, StringComparison.OrdinalIgnoreCase) < 0)
				{
					continue;
				}
				var candidates = ParameterCandidates(parameter, variables, literals, numbers);
				if (candidates == null)
				{
					logger.LogInformation($"{step.LineNumber}: Decider leaves parameters to llm, {parameter.Name} ({parameter.Type}) is not a choice");
					return null;
				}
				questions[parameter.Name] = candidates;
			}

			var returnType = method.ReturnValue?.Type;
			bool hasReturn = !string.IsNullOrEmpty(returnType) && !returnType.EndsWith("Void", StringComparison.OrdinalIgnoreCase);
			if (hasReturn)
			{
				var targets = variables.ToDictionary(v => v, v => $"write the result into the variable {v}");
				targets[NoneOption] = "the result is not written to a variable";
				questions["__return__"] = targets;
			}
			if (questions.Count == 0)
			{
				return BuildDecided(step, method, new(), new(), question);
			}

			var (choices, error) = await decider.ChooseParameters(step.Text, method.MethodName, questions);
			if (error != null || choices == null)
			{
				logger.LogWarning($"{step.LineNumber}: Decider could not choose parameters, llm fills them: {error?.Message}");
				return null;
			}

			var parameters = new List<Parameter>();
			foreach (var parameter in method.Parameters ?? new())
			{
				// Never asked about, so nothing to trust or distrust: the parameter stays unset.
				if (!questions.ContainsKey(parameter.Name)) continue;

				if (!choices.TryGetValue(parameter.Name, out var choice) || choice.Confidence < PLang.Building.BuilderDecider.ConfidenceThreshold)
				{
					logger.LogInformation($"{step.LineNumber}: Decider parameter {parameter.Name}={choice?.Choice} ({choice?.Confidence:0.00}) not trusted, llm fills parameters");
					return null;
				}
				if (choice.Choice == NoneOption)
				{
					if (parameter.IsRequired)
					{
						logger.LogInformation($"{step.LineNumber}: Decider found no value in the step for required parameter {parameter.Name}, llm fills parameters");
						return null;
					}
					continue;
				}
				var value = ToValue(parameter, choice.Choice);
				if (value == null)
				{
					logger.LogInformation($"{step.LineNumber}: Decider could not type {parameter.Name}={choice.Choice} as {parameter.Type}, llm fills parameters");
					return null;
				}
				logger.LogInformation($"{step.LineNumber}: Decider set {parameter.Name} = {choice.Choice} ({choice.Confidence:0.00})");
				parameters.Add(new Parameter(parameter.Type, parameter.Name, value));
			}

			var returnValues = new List<ReturnValue>();
			if (hasReturn && choices.TryGetValue("__return__", out var target))
			{
				if (target.Confidence < PLang.Building.BuilderDecider.ConfidenceThreshold)
				{
					logger.LogInformation($"{step.LineNumber}: Decider return target {target.Choice} ({target.Confidence:0.00}) not trusted, llm fills parameters");
					return null;
				}
				if (target.Choice != NoneOption)
				{
					logger.LogInformation($"{step.LineNumber}: Decider set return to {target.Choice} ({target.Confidence:0.00})");
					returnValues.Add(new ReturnValue(returnType!, target.Choice));
				}
			}

			return BuildDecided(step, method, parameters, returnValues, question);
		}

		private Instruction BuildDecided(GoalStep step, MethodDescription method, List<Parameter> parameters, List<ReturnValue> returnValues, LlmRequest question)
		{
			logger.LogInformation($"{step.LineNumber}: Decider built {method.MethodName} without the llm");
			var function = new GenericFunction("Decided by Typesafe: module, method and parameters", method.MethodName, parameters, returnValues);
			return InstructionCreator.Create(function, step, question);
		}

		// The finite options for one parameter, keyed by the text the decider hands back. Null
		// means the type is not something the step can answer by choosing, so the step goes to
		// the llm.
		private static Dictionary<string, string>? ParameterCandidates(IPropertyDescription parameter, List<string> variables, List<string> literals, List<string> numbers)
		{
			var type = parameter.Type ?? "";
			var candidates = new Dictionary<string, string>();

			if (type == "System.Boolean" || type == "System.Nullable`1[System.Boolean]")
			{
				candidates["true"] = "true";
				candidates["false"] = "false";
			}
			else if (ResolveType(type) is Type enumType && enumType.IsEnum)
			{
				foreach (var name in Enum.GetNames(enumType)) candidates[name] = $"the value {name}";
			}
			else if (type == "System.String" || type == "System.Object")
			{
				foreach (var literal in literals) candidates[literal] = $"the text \"{literal}\" written in the step";
				foreach (var variable in variables) candidates[variable] = $"the variable {variable}";
			}
			else if (IsNumeric(type))
			{
				foreach (var number in numbers) candidates[number] = $"the number {number} written in the step";
				foreach (var variable in variables) candidates[variable] = $"the variable {variable}";
			}
			else
			{
				return null;
			}

			if (candidates.Count == 0) return null;
			candidates[NoneOption] = parameter.IsRequired
				? "none of these, the value is something else"
				: "none of these, leave the parameter unset so its default applies";
			return candidates;
		}

		private static object? ToValue(IPropertyDescription parameter, string choice)
		{
			if (choice.StartsWith("%") && choice.EndsWith("%")) return choice;

			var type = parameter.Type ?? "";
			if (type == "System.Boolean" || type == "System.Nullable`1[System.Boolean]")
			{
				return bool.TryParse(choice, out var b) ? b : null;
			}
			if (ResolveType(type) is Type enumType && enumType.IsEnum) return choice;
			if (type == "System.String" || type == "System.Object") return choice;
			if (IsNumeric(type))
			{
				var culture = System.Globalization.CultureInfo.InvariantCulture;
				if (type.Contains("Int64")) return long.TryParse(choice, out var l) ? l : null;
				if (type.Contains("Int32")) return int.TryParse(choice, out var i) ? i : null;
				return double.TryParse(choice, System.Globalization.NumberStyles.Any, culture, out var d) ? d : null;
			}
			return null;
		}

		private static bool IsNumeric(string type)
		{
			return type == "System.Int32" || type == "System.Int64" || type == "System.Double" || type == "System.Single" || type == "System.Decimal"
				|| type == "System.Nullable`1[System.Int32]" || type == "System.Nullable`1[System.Int64]" || type == "System.Nullable`1[System.Double]";
		}

		private static Type? ResolveType(string typeName)
		{
			if (string.IsNullOrEmpty(typeName) || typeName.StartsWith("System.")) return null;
			var type = Type.GetType(typeName);
			if (type != null) return type;
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				type = assembly.GetType(typeName);
				if (type != null) return type;
			}
			return null;
		}

		private static List<string> QuotedLiterals(string text)
		{
			var literals = new List<string>();
			foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text ?? "", "\"([^\"]*)\"|'([^']*)'"))
			{
				var literal = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
				if (!literals.Contains(literal)) literals.Add(literal);
			}
			return literals;
		}

		private static List<string> Numbers(string text)
		{
			var withoutLiterals = System.Text.RegularExpressions.Regex.Replace(text ?? "", "\"[^\"]*\"|'[^']*'|%[^%]*%", " ");
			var numbers = new List<string>();
			foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(withoutLiterals, @"(?<![\w.])-?\d+(?:\.\d+)?(?![\w.])"))
			{
				if (!numbers.Contains(match.Value)) numbers.Add(match.Value);
			}
			return numbers;
		}

		public virtual LlmRequest GetLlmRequest(GoalStep step, Type responseType, IBuilderError? previousBuildError = null, ClassDescription? classDescription = null)
		{
			var promptMessage = new List<LlmMessage>();

			if (string.IsNullOrEmpty(system))
			{
				system = GetDefaultSystemText(step);
			}
			var systemContent = new List<LlmContent>();
			systemContent.Add(new LlmContent(system));
			foreach (var append in appendedSystemCommand)
			{
				systemContent.Add(new LlmContent(append));
			}

			promptMessage.Add(new LlmMessage("system", string.Join("\n", systemContent.Select(p => p.Text))));

			var assistantContent = new List<LlmContent>();
			if (!string.IsNullOrEmpty(step.UserIntent))
			{
				assistantContent.Add(new LlmContent($"## Canonical User Intent (already disambiguated by LLM)\r\n{step.UserIntent}"));
			}

			if (string.IsNullOrEmpty(assistant))
			{
				(assistant, var error) = GetDefaultAssistantText(step, previousBuildError, classDescription);
				if (error != null) throw new ExceptionWrapper(error);
			}

			assistantContent.Add(new LlmContent(assistant));
			foreach (var append in appendedAssistantCommand)
			{
				assistantContent.Add(new LlmContent(append));
			}
			if (assistantContent.Count > 0)
			{
				promptMessage.Add(new LlmMessage("assistant", string.Join("\n", assistantContent.Select(p => p.Text))));
			}

			var userContent = new List<LlmContent>();
			string user = step.LlmText ?? step.Text;
			user = user.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t");
			userContent.Add(new LlmContent(user));
			promptMessage.Add(new LlmMessage("user", userContent));

			if (previousBuildError != null || step.ValidationErrors.Count > 0)
			{
				if (step.ValidationErrors.Count > 0)
				{
					if (previousBuildError == null) previousBuildError = new BuilderError("");
					previousBuildError.ErrorChain.AddRange(step.ValidationErrors);
				}

				string errorInfoForLlm = ErrorHelper.MakeForLlm(previousBuildError);
				promptMessage.Add(new LlmMessage("assistant", errorInfoForLlm));
				//step.Reload = true;

				// upgrade the model since there was error
				model = "gpt-4o";
			}


			var llmRequest = new LlmRequest(GetType().FullName, promptMessage);
			llmRequest.Step = step;
			llmRequest.Goal = step.Goal;

			llmRequest.Reload = step.Reload;
			if (contentExtractor != null)
			{
				llmRequest.llmResponseType = contentExtractor.LlmResponseType;
			}
			llmRequest.scheme = TypeHelper.GetJsonSchema(responseType);
			llmRequest.top_p = 0;
			llmRequest.temperature = 0;
			llmRequest.frequencyPenalty = 0;
			llmRequest.presencePenalty = 0;
			if (model != null)
			{
				llmRequest.model = model;
			}

			return llmRequest;

		}

		[Method]
		public string GetDefaultSystemText(GoalStep step)
		{
			/*
			 * file.read system.txt, write to %content%
			 * write out content
			 * */
			return $@"User is writing code in plang programming language.

Your job is: 
1. Parse and understand user intent
2. Map the intent to one of C# function provided to you
3. Return a valid JSON

## Rules
%variable% in plang is defined with starting and ending percentage sign (%)
%variable% MUST be wrapped in quotes("") in json response, e.g. {{ ""name"":""%name%"" }}
leave %variable% as is and do not change text to a variable
null is used to represent no value, e.g. {{ ""name"": null }}
Variables MUST not be changed, they can include dot(.) and parentheses()
Keep \n, \r, \t that are submitted to you for string variables
Parameters that is type System.String MUST be without escaping quotes. See <Example>
Error handling is process by another step, if you see 'on error...' you can ignore it
If there is some api key, settings, config replace it with %Settings.NameOfApiKey% 
- NameOfApiKey should named in relation to what is happening if change is needed
Dictionary<T1, T2> value is {{key: value, ... }} => a dictionary parameter defined as %variable% without key should have the same key and value as %variable%, e.g. %userId% => {{ key: ""userId"", value:""%userId%""}}
Variable with ToString with date/time formatting, assume it is System.DateTime, e.g. %updated.ToString(""yyyy-MM-dd"")% then type of %updated% is System.DateTime 
List, ReadOnlyList are array of the object => e.g. user defines single property for List, return it as array
When you see t%variable% or t""this is text"", set the Type to Plang.TString. This is for translation
<Example>
get url ""http://example.org"" => Value: ""http://example.org""
write out 'Hello world' => Value: ""Hello world""
<Example>

## JSON scheme information
Reasoning: A brief description of the reasoning behind the selection of the function and parameters based on the user's intent. This property provides context for why a particular function was chosen and how it aligns with the user intent.
Name: Name of the function to use from list of functions, if no function matches set as ""N/A""
Parameters: List of parameters that are needed according to the user intent.
- Type: the object type in c#
- Name: name of the variable
- Value: ""%variable%"", hardcode string that should be used, null, or json array/object
ReturnValue rules
- Only if the function returns a value AND if user defines %variable% to write into, e.g. ' write into %data%' or 'into %result%', or simliar intent to write return value into variable
- If no %variable% is defined then set as null.
".Trim();
		}
		[Method]
		/*
		 * TODO: SignatureInfo should be append to each return, roslyn? who can do it? #good-first-issue
		 * */
		public (string?, IBuilderError?) GetDefaultAssistantText(GoalStep step, IBuilderError? previousBuildError = null, ClassDescription? classDescription = null)
		{
			var programType = typeHelper.GetRuntimeType(module);
			if (programType == null) return (null, new StepBuilderError($"Could not load type {module}", step));

			var variables = GetVariablesInStep(step).Replace("%", "");

			if (classDescription == null)
			{
				var classDescriptionHelper = new ClassDescriptionHelper();
				(classDescription, var error) = classDescriptionHelper.GetClassDescription(programType);
				if (error != null) return (null, error);
			}

			string assistant = "";
			if (classDescription != null)
			{
				var json = JsonConvert.SerializeObject(classDescription, new JsonSerializerSettings
				{
					NullValueHandling = NullValueHandling.Ignore
				});
				assistant = $@"
## functions available starts ##
{json}
## functions available ends ##";
			}

			if (!string.IsNullOrEmpty(variables))
			{
				assistant += @$"
## defined variables ##
{variables}
## defined variables ##";
			}
			return (assistant.Trim(), null);
		}

		[Method]
		public string GetVariablesInStep(GoalStep step)
		{
			var variables = variableHelper.GetVariables(step.Text, memoryStack).DistinctBy(p => p.PathAsVariable);
			string vars = "";

			// todo: hack, why is Goal null?
			memoryStack.Goal = step.Goal;

			foreach (var variable in variables)
			{
				if (variable.Initiated && !variable.Name.StartsWith("Settings"))
				{
					vars += variable.PathAsVariable + " (" + variable.Value + "), ";
				}
				else
				{
					vars += variable.PathAsVariable + " (type:" + (variable.Value?.GetType().FullName ?? "object") + "), ";

				}
			}
			return vars;
		}



	}


}
