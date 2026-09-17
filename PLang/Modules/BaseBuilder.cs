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
		private PLang.Building.IBuilderDeciderCache? deciderCache;
		protected GoalStep GoalStep;


#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		protected BaseBuilder()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		{ }



		[Init]
		public void InitBaseBuilder(GoalStep goalStep, IPLangFileSystem fileSystem, ILlmServiceFactory llmServiceFactory, ITypeHelper typeHelper,
			MemoryStack memoryStack, PLangContext context, VariableHelper variableHelper, ILogger logger, PLang.Building.IBuilderDecider? decider = null, PLang.Building.IBuilderDeciderCache? deciderCache = null)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			this.decider = decider;
			this.deciderCache = deciderCache;
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

			// Usually already answered for the whole goal in one request by StepBuilder's prefetch.
			MethodChoice? choice;
			IError? deciderError = null;
			var prefetched = deciderCache?.ForGoal(step.Goal!).Methods;
			if (prefetched != null && prefetched.TryGetValue(step.Index, out var cachedChoice))
			{
				choice = cachedChoice;
			}
			else
			{
				(choice, deciderError) = await decider.ChooseMethod(step.Text, module, MethodCriteria(classDescription));
			}
			if (deciderError != null)
			{
				logger.LogWarning($"{step.LineNumber}: Decider could not choose a method, llm picks from all methods: {deciderError.Message}");
				return null;
			}

			var overloads = classDescription.Methods.Where(m => m.MethodName == choice?.Method).ToList();
			if (choice == null || overloads.Count == 0 || choice.Confidence < PLang.Building.BuilderDecider.ConfidenceThreshold)
			{
				var contenders = string.Join(", ", (choice?.Probabilities ?? new()).OrderByDescending(p => p.Value).Take(4).Select(p => $"{p.Key} {p.Value:0.00}"));
				logger.LogInformation($"{step.LineNumber}: Decider method {choice?.Method} ({choice?.Confidence:0.00}) not trusted, llm picks from all methods. Contenders: {contenders}");
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
		// Shared with StepBuilder's prefetch so the batched question and the per step question can
		// never offer different options for the same method choice.
		internal static Dictionary<string, string> MethodCriteria(ClassDescription classDescription)
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

			var questions = new Dictionary<string, ParameterQuestion>();
			var recordFields = new Dictionary<string, List<PrimitiveDescription>>();
			foreach (var parameter in method.Parameters ?? new())
			{
				if (LeaveUnset(parameter, step.Text)) continue;
				var candidates = ParameterCandidates(parameter, variables, literals, numbers);
				if (candidates == null)
				{
					// A record of simple fields (TextMessage is one) is decided field by field, the same
					// way a method is decided parameter by parameter: required fields are asked, fields
					// with a constructor default that the step never mentions keep that default.
					var fields = RecordFields(parameter, step.Text);
					if (fields == null)
					{
						logger.LogInformation($"{step.LineNumber}: Decider leaves parameters to llm, {parameter.Name} ({parameter.Type}) is not a choice");
						return null;
					}
					foreach (var field in fields)
					{
						var fieldCandidates = ParameterCandidates(field, variables, literals, numbers);
						if (fieldCandidates == null)
						{
							logger.LogInformation($"{step.LineNumber}: Decider leaves parameters to llm, {parameter.Name}.{field.Name} ({field.Type}) is not a choice");
							return null;
						}
						if (fieldCandidates.Count == 0) continue;
						questions[$"{parameter.Name}.{field.Name}"] = new ParameterQuestion(Describe(field, $"a field of the {parameter.Name} parameter"), fieldCandidates);
					}
					recordFields[parameter.Name] = fields;
					continue;
				}
				if (candidates.Count == 0) continue;
				questions[parameter.Name] = new ParameterQuestion(Describe(parameter, null), candidates);
			}

			// Whether the step captures its result is settled first, on its own, because other
			// parameters depend on it and questions in one request cannot see each other's answers:
			// RenderTemplate's RenderToOutputstream has to be true exactly when nothing is captured,
			// and asked blind it stayed unset, which renders the page and sends nothing. The answer
			// is added to the state so the rest of the parameters are decided knowing it.
			var returnType = method.ReturnValue?.Type;
			bool hasReturn = !string.IsNullOrEmpty(returnType) && !returnType.EndsWith("Void", StringComparison.OrdinalIgnoreCase);
			var state = step.Text;
			ParameterChoice? returnChoice = null;

			if (hasReturn)
			{
				var targets = variables.ToDictionary(v => v, v => $"write the result into the variable {v}");
				targets[NoneOption] = "the result is not written to a variable";
				var returnQuestion = new Dictionary<string, ParameterQuestion>
				{
					["__return__"] = new ParameterQuestion("The variable the method's result is written into, when the step names one (write to %x%, into %x%, -> %x%).", targets)
				};

				var (returnAnswer, returnError) = await decider.ChooseParameters(state, method.MethodName, returnQuestion);
				if (returnError != null || returnAnswer == null || !returnAnswer.TryGetValue("__return__", out returnChoice))
				{
					logger.LogWarning($"{step.LineNumber}: Decider could not choose the return variable, llm fills parameters: {returnError?.Message}");
					return null;
				}
				state += returnChoice.Choice == NoneOption
					? "\n\nThis step does not write its result into any variable."
					: $"\n\nThis step writes its result into the variable {returnChoice.Choice}.";
			}

			if (questions.Count == 0)
			{
				// Nothing to fill, but the return variable was still decided above and must be kept.
				var onlyReturn = new List<ReturnValue>();
				if (returnChoice != null && returnChoice.Choice != NoneOption)
				{
					if (returnChoice.Confidence < PLang.Building.BuilderDecider.ConfidenceThreshold)
					{
						logger.LogInformation($"{step.LineNumber}: Decider return target {returnChoice.Choice} ({returnChoice.Confidence:0.00}) not trusted, llm fills parameters");
						return null;
					}
					onlyReturn.Add(new ReturnValue(returnType!, returnChoice.Choice));
				}
				if (!EveryValuePlaced(step, variables, literals, numbers, new(), onlyReturn)) return null;
				return BuildDecided(step, method, new(), onlyReturn, question);
			}

			var (choices, error) = await decider.ChooseParameters(state, method.MethodName, questions);
			if (error != null || choices == null)
			{
				logger.LogWarning($"{step.LineNumber}: Decider could not choose parameters, llm fills them: {error?.Message}");
				return null;
			}
			if (returnChoice != null) choices["__return__"] = returnChoice;

			choices = DropDuplicateClaims(step, choices);

			var parameters = new List<Parameter>();
			foreach (var parameter in method.Parameters ?? new())
			{
				if (recordFields.TryGetValue(parameter.Name, out var fields))
				{
					var record = new Dictionary<string, object?>();
					foreach (var field in fields)
					{
						var fieldKey = $"{parameter.Name}.{field.Name}";
						// Nothing in the step could fill it, so it was never asked and keeps its default.
						if (!questions.ContainsKey(fieldKey)) continue;
						if (!choices.TryGetValue(fieldKey, out var fieldChoice)) return null;
						if (fieldChoice.Choice == NoneOption && !field.IsRequired)
						{
							if (fieldChoice.Confidence < PLang.Building.BuilderDecider.ConfidenceThreshold)
							{
								logger.LogInformation($"{step.LineNumber}: Decider is unsure whether to leave {fieldKey} unset ({fieldChoice.Confidence:0.00}), llm fills parameters");
								return null;
							}
							logger.LogDebug($"{step.LineNumber}: Decider leaves {fieldKey} unset ({fieldChoice.Confidence:0.00})");
							continue;
						}
						if (fieldChoice.Confidence < PLang.Building.BuilderDecider.ConfidenceThreshold)
						{
							logger.LogInformation($"{step.LineNumber}: Decider field {fieldKey}={fieldChoice.Choice} ({fieldChoice.Confidence:0.00}, {(field.IsRequired ? "required" : "optional")}) not trusted, llm fills parameters");
							return null;
						}
						if (fieldChoice.Choice == NoneOption)
						{
							logger.LogInformation($"{step.LineNumber}: Decider found no value in the step for required field {fieldKey}, llm fills parameters");
							return null;
						}
						var fieldValue = ToValue(field, fieldChoice.Choice);
						if (fieldValue == null)
						{
							logger.LogInformation($"{step.LineNumber}: Decider could not type {fieldKey}={fieldChoice.Choice} as {field.Type}, llm fills parameters");
							return null;
						}
						logger.LogInformation($"{step.LineNumber}: Decider set {fieldKey} = {fieldChoice.Choice} ({fieldChoice.Confidence:0.00})");
						record[field.Name] = fieldValue;
					}
					var recordValue = RecordValue(parameter, record);
					if (recordValue == null)
					{
						logger.LogInformation($"{step.LineNumber}: Decider could not construct {parameter.Name} ({parameter.Type}), llm fills parameters");
						return null;
					}
					parameters.Add(new Parameter(parameter.Type, parameter.Name, recordValue));
					continue;
				}

				// Never asked about, so nothing to trust or distrust: the parameter stays unset.
				if (!questions.ContainsKey(parameter.Name)) continue;

				// Leaving a parameter unset is an answer like any other and has to be as sure as an
				// assignment. Taking it at any confidence was how a flag the module documents as
				// "true when the step captures no result" stayed false at 0.37 confidence and built a
				// page that renders and sends nothing. The engine is decisive when it knows: the
				// unsets that are right come back at 0.91 to 1.00.
				if (!choices.TryGetValue(parameter.Name, out var choice)) return null;
				if (choice.Choice == NoneOption && !parameter.IsRequired)
				{
					if (choice.Confidence < PLang.Building.BuilderDecider.ConfidenceThreshold)
					{
						logger.LogInformation($"{step.LineNumber}: Decider is unsure whether to leave {parameter.Name} unset ({choice.Confidence:0.00}), llm fills parameters");
						return null;
					}
					continue;
				}
				if (choice.Confidence < PLang.Building.BuilderDecider.ConfidenceThreshold)
				{
					logger.LogInformation($"{step.LineNumber}: Decider parameter {parameter.Name}={choice.Choice} ({choice.Confidence:0.00}) not trusted, llm fills parameters");
					return null;
				}
				if (choice.Choice == NoneOption)
				{
					logger.LogInformation($"{step.LineNumber}: Decider found no value in the step for required parameter {parameter.Name}, llm fills parameters");
					return null;
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

			if (!EveryValuePlaced(step, variables, literals, numbers, parameters, returnValues)) return null;
			return BuildDecided(step, method, parameters, returnValues, question);
		}

		// Every question is answered on its own, so one value in the step can be claimed by several
		// parameters at once: `render "x.html", write to %html%` had %html% as the return variable at
		// 0.95 and as the render target at 0.31. A value written once in a step belongs to one
		// parameter, so the surest claim keeps it and the rest fall back to unset, which the loops
		// below then reject for a required parameter and accept for an optional one.
		private Dictionary<string, ParameterChoice> DropDuplicateClaims(GoalStep step, Dictionary<string, ParameterChoice> choices)
		{
			var winners = choices
				.Where(c => c.Value.Choice != NoneOption)
				.GroupBy(c => c.Value.Choice)
				.Where(g => g.Count() > 1)
				.Select(g => g.OrderByDescending(c => c.Value.Confidence).First().Key)
				.ToHashSet();
			if (winners.Count == 0) return choices;

			var deduped = new Dictionary<string, ParameterChoice>();
			foreach (var choice in choices)
			{
				bool contested = choices.Any(other => other.Key != choice.Key && other.Value.Choice == choice.Value.Choice);
				if (choice.Value.Choice != NoneOption && contested && !winners.Contains(choice.Key))
				{
					logger.LogInformation($"{step.LineNumber}: Decider gives {choice.Value.Choice} to another parameter, {choice.Key} is left unset");
					deduped[choice.Key] = choice.Value with { Choice = NoneOption };
					continue;
				}
				deduped[choice.Key] = choice.Value;
			}
			return deduped;
		}

		// The step's own values, its variables, quoted texts and numbers, must all have landed in a
		// parameter or the return variable; one left over means the step carries something the
		// decided method has no place for, and the llm has to read it. This is what stops a step
		// like `set %a% = %x%, %b% = %y%` from being built with its only parameter, a dictionary
		// that is not a choice, left out because it is declared optional.
		private bool EveryValuePlaced(GoalStep step, List<string> variables, List<string> literals, List<string> numbers, List<Parameter> parameters, List<ReturnValue> returnValues)
		{
			var placed = new List<string>();
			foreach (var parameter in parameters)
			{
				if (parameter.Value is Newtonsoft.Json.Linq.JObject record)
				{
					placed.AddRange(record.Properties().Select(p => p.Value.ToString()));
				}
				else if (parameter.Value != null)
				{
					placed.Add(parameter.Value.ToString()!);
				}
			}
			placed.AddRange(returnValues.Select(r => r.VariableName));

			var unplaced = variables.Concat(literals).Concat(numbers)
				.Where(value => !placed.Any(p => p.Contains(value, StringComparison.OrdinalIgnoreCase)))
				.ToList();
			if (unplaced.Count == 0) return true;

			logger.LogInformation($"{step.LineNumber}: Decider found no parameter for {string.Join(", ", unplaced)} in the step, llm builds it");
			return false;
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
			else if (IsStringList(type))
			{
				foreach (var literal in literals) candidates[literal] = $"a list holding only \"{literal}\"";
				foreach (var variable in variables) candidates[variable] = $"a list holding only the variable {variable}";
			}
			else
			{
				return null;
			}

			// Nothing in the step could fill it. A required parameter then has to go to the llm, but an
			// optional one simply keeps its default: RenderMessage.StatusCode is an int and most steps
			// write no number at all, which was being reported as "not a choice" and gave up the step.
			// An empty set of candidates, as opposed to null, tells the caller to leave it unset.
			if (candidates.Count == 0) return parameter.IsRequired ? null : candidates;
			// Saying what unset means matters: choosing between "true" and "leave it unset" is only a
			// real choice when the engine knows unset is false. RenderToOutputstream stayed unset on
			// steps whose description said it should be true, because "its default applies" named no
			// value to compare against.
			var unsetMeans = parameter.DefaultValue == null ? "" : $", which means {parameter.Name} = {parameter.DefaultValue}";
			candidates[NoneOption] = parameter.IsRequired
				? "none of these, the value is something else"
				: $"none of these, leave the parameter unset so its default applies{unsetMeans}";
			return candidates;
		}

		// The fields of a record parameter, read off its constructor: a field with no default is
		// required, one with a default is only asked about when the step names it. Null when the
		// type cannot be resolved or has no constructor to read.
		private static List<PrimitiveDescription>? RecordFields(IPropertyDescription parameter, string stepText)
		{
			var fields = new List<PrimitiveDescription>();
			return CollectRecordFields(parameter.Type ?? "", "", stepText, fields, 0) ? fields : null;
		}

		// Walks a record's constructor and collects the leaves that can be decided, naming each by
		// its dotted path. A record inside a record is walked too: `render` takes a
		// RenderTemplateOptions holding a RenderMessage, so the questions are
		// options.RenderMessage.Content and options.RenderMessage.Target. Depth is capped because a
		// type graph can be deep or cyclic, and a leaf that is neither a choice nor a record it can
		// walk gives up on the whole step.
		private static bool CollectRecordFields(string typeName, string prefix, string stepText, List<PrimitiveDescription> fields, int depth)
		{
			if (depth > 2) return false;

			var type = ResolveType(typeName);
			var constructor = type?.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
			if (constructor == null || constructor.GetParameters().Length == 0) return false;

			// The record's own [Description] is what explains its fields ("Content can be a filename
			// or a text that will be written to stream"), and without it a field is just a name and a
			// type: RenderMessage.Content scored 0.35 on the only file path in the step. Each field
			// gets the lines about itself, not the whole text: handing all seven fields' worth of
			// TextMessage prose to a question about Content dropped it from 0.79 to 0.66.
			// GetCustomAttribute, singular, throws AmbiguousMatchException when a type carries its own
			// [Description] and inherits one, which RenderMessage does through OutMessage. Join them,
			// the way TypeHelper joins a module's descriptions.
			var recordDescription = string.Join("\n", System.Reflection.CustomAttributeExtensions
				.GetCustomAttributes(type!, typeof(System.ComponentModel.DescriptionAttribute))
				.Cast<System.ComponentModel.DescriptionAttribute>().Select(a => a.Description));
			var perField = SplitDescriptionByField(recordDescription, constructor.GetParameters().Select(p => p.Name!).ToList());

			foreach (var field in constructor.GetParameters())
			{
				if (System.Reflection.CustomAttributeExtensions.GetCustomAttribute(field, typeof(PLang.Attributes.LlmIgnoreAttribute)) != null) continue;

				var fieldType = Nullable.GetUnderlyingType(field.ParameterType) ?? field.ParameterType;
				var path = prefix.Length == 0 ? field.Name! : $"{prefix}.{field.Name}";
				var description = new PrimitiveDescription
				{
					Name = path,
					Type = fieldType.FullName ?? "",
					IsRequired = !field.HasDefaultValue,
					DefaultValue = field.HasDefaultValue ? field.DefaultValue : null,
					Description = string.Join(" ", new[]
					{
						FieldDescription(type!, field),
						perField.GetValueOrDefault(field.Name!)
					}.Where(text => !string.IsNullOrWhiteSpace(text)))
				};

				if (LeaveUnset(description, stepText)) continue;
				if (IsChoiceType(description.Type))
				{
					fields.Add(description);
					continue;
				}
				if (!CollectRecordFields(description.Type, path, stepText, fields, depth + 1)) return false;
			}
			return true;
		}

		// A record's fields are documented in two places and both have to be read. On a positional
		// record the attribute is usually written [property: Description(...)], which lands on the
		// generated property, not on the constructor parameter: RenderToOutputstream carried a
		// description saying exactly when it must be true, the parameter had none, and the decider
		// left the flag unset and rendered pages that sent nothing.
		private static string FieldDescription(Type type, System.Reflection.ParameterInfo field)
		{
			var descriptions = System.Reflection.CustomAttributeExtensions
				.GetCustomAttributes(field, typeof(System.ComponentModel.DescriptionAttribute))
				.Cast<System.ComponentModel.DescriptionAttribute>().Select(a => a.Description).ToList();

			var property = type.GetProperty(field.Name!);
			if (property != null)
			{
				descriptions.AddRange(System.Reflection.CustomAttributeExtensions
					.GetCustomAttributes(property, typeof(System.ComponentModel.DescriptionAttribute))
					.Cast<System.ComponentModel.DescriptionAttribute>().Select(a => a.Description));
			}
			return string.Join(" ", descriptions.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct());
		}

		// A record description is written a line per field ("Content can be...", "Target defines...").
		// Split it back up: a line opening with a field name starts that field's text, and the lines
		// under it belong to it too, which is how the list of built in Actions stays with Actions.
		private static Dictionary<string, string> SplitDescriptionByField(string? description, List<string> fieldNames)
		{
			var perField = new Dictionary<string, List<string>>();
			if (string.IsNullOrWhiteSpace(description)) return new();

			string? current = null;
			foreach (var line in description.Split('\n'))
			{
				var text = line.Trim();
				if (text.Length == 0) continue;

				var opener = fieldNames.FirstOrDefault(name =>
					text.StartsWith(name, StringComparison.OrdinalIgnoreCase)
					&& (text.Length == name.Length || !char.IsLetterOrDigit(text[name.Length])));
				if (opener != null) current = opener;
				if (current == null) continue;

				if (!perField.TryGetValue(current, out var lines)) perField[current] = lines = new();
				lines.Add(text);
			}
			return perField.ToDictionary(p => p.Key, p => string.Join(" ", p.Value));
		}

		// Whether a type can be answered by choosing, without needing the step's values to know.
		private static bool IsChoiceType(string type)
		{
			return type == "System.Boolean" || type == "System.Nullable`1[System.Boolean]"
				|| type == "System.String" || type == "System.Object"
				|| IsNumeric(type) || IsStringList(type)
				|| (ResolveType(type) is Type enumType && enumType.IsEnum);
		}

		// A list of strings, e.g. the Actions of a render message. One option is chosen and becomes a
		// one element list; a step naming several (`replace the content, navigate and scroll`) leaves
		// the others unplaced, and EveryValuePlaced then hands the step to the llm.
		private static bool IsStringList(string type)
		{
			return (type.StartsWith("System.Collections.Generic.List`1[")
				|| type.StartsWith("System.Collections.Generic.IReadOnlyList`1[")
				|| type.StartsWith("System.Collections.Generic.IList`1["))
				&& type.Contains("System.String");
		}

		// The runtime fills a record parameter property by property, never through its constructor,
		// so a field left out does not get its constructor default but default(T): a TextMessage
		// with only Content set arrives with Level null and StatusCode 0 and the sink throws. Build
		// the real instance through the constructor, decided fields plus defaults, and write that,
		// which is the same complete shape the llm writes.
		private static object? RecordValue(IPropertyDescription parameter, Dictionary<string, object?> decided)
		{
			var instance = ConstructRecord(parameter.Type ?? "", "", decided);
			return instance == null ? null : Newtonsoft.Json.Linq.JObject.FromObject(instance);
		}

		private static object? ConstructRecord(string typeName, string prefix, Dictionary<string, object?> decided)
		{
			var type = ResolveType(typeName);
			var constructor = type?.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
			if (constructor == null) return null;

			var arguments = new List<object?>();
			foreach (var field in constructor.GetParameters())
			{
				var path = prefix.Length == 0 ? field.Name! : $"{prefix}.{field.Name}";
				var fieldType = Nullable.GetUnderlyingType(field.ParameterType) ?? field.ParameterType;

				if (decided.TryGetValue(path, out var value) && value != null)
				{
					arguments.Add(CoerceToFieldType(value, fieldType));
				}
				else if (decided.Keys.Any(key => key.StartsWith(path + ".", StringComparison.Ordinal)))
				{
					var nested = ConstructRecord(fieldType.FullName ?? "", path, decided);
					if (nested == null) return null;
					arguments.Add(nested);
				}
				else
				{
					arguments.Add(field.HasDefaultValue ? field.DefaultValue : null);
				}
			}
			return constructor.Invoke(arguments.ToArray());
		}

		private static object? CoerceToFieldType(object value, Type fieldType)
		{
			if (fieldType.IsEnum) return Enum.Parse(fieldType, value.ToString()!);
			if (IsStringList(fieldType.FullName ?? "")) return value is string single ? new List<string> { single } : value;
			return Convert.ChangeType(value, fieldType, System.Globalization.CultureInfo.InvariantCulture);
		}

		// An optional parameter the step never names is left unset, so its default applies, unless
		// it is a text, number or object: those take their candidates from the step itself, and
		// `set %greeting% = "hello"` never names its `value` parameter, yet that is where "hello"
		// goes. A bool or enum has candidates regardless of the step, and a complex type is not a
		// choice at all; asking about either invites an unsure answer that vetoes the whole step.
		// Leaving a complex one unset is only safe because EveryValuePlaced then refuses a step
		// whose values found no home.
		// What the decider is told about a parameter: whether it may be left unset, its type, and
		// the description the module author wrote for it, the same text the llm gets.
		private static string Describe(IPropertyDescription parameter, string? role)
		{
			var type = (parameter.Type ?? "").Replace("System.Nullable`1[", "").Replace("System.", "").TrimEnd(']');
			// Nothing here may argue for a particular answer. This used to end "left unset unless the
			// step clearly gives it a value", which overrode the parameter's own documentation: a flag
			// the module says must be true when the step captures no result stayed unset, because the
			// step never writes the word true. What unset means is said once, on that option itself.
			var text = parameter.IsRequired
				? $"required, type {type}."
				: $"optional, type {type}.";
			if (role != null) text = role + ", " + text;
			if (!string.IsNullOrWhiteSpace(parameter.Description)) text += " " + parameter.Description.Trim();
			return text;
		}

		// Anything that can be answered by choosing is always asked, even when the step never names
		// it: a step says "without main layout", not "DontRenderMainLayout", and skipping the bool
		// because its name is absent built a render step with DontRenderMainLayout false where the
		// llm set it true. That is the one failure worse than falling back, a step that builds and
		// then quietly does the wrong thing. An unsure answer only costs a fallback, now that unset
		// is accepted at any confidence.
		//
		// What is still skipped is an optional parameter of a type that is not a choice at all, an
		// object or a list of objects the step never mentions. That is safe only because
		// EveryValuePlaced refuses the step when a value written in it found no home.
		private static bool LeaveUnset(IPropertyDescription parameter, string stepText)
		{
			if (parameter.IsRequired) return false;
			if (IsChoiceType(parameter.Type ?? "")) return false;
			return stepText.IndexOf(parameter.Name, StringComparison.OrdinalIgnoreCase) < 0;
		}

		private static object? ToValue(IPropertyDescription parameter, string choice)
		{
			var listType = parameter.Type ?? "";
			if (IsStringList(listType)) return new List<string> { choice };

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
