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
		private PLang.Building.IBuilderDeciderReport? deciderReport;
		private List<string>? goalNames;
		protected GoalStep GoalStep;


#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		protected BaseBuilder()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		{ }



		[Init]
		public void InitBaseBuilder(GoalStep goalStep, IPLangFileSystem fileSystem, ILlmServiceFactory llmServiceFactory, ITypeHelper typeHelper,
			MemoryStack memoryStack, PLangContext context, VariableHelper variableHelper, ILogger logger, PLang.Building.IBuilderDecider? decider = null, PLang.Building.IBuilderDeciderCache? deciderCache = null, PLang.Building.IBuilderDeciderReport? deciderReport = null, List<string>? goalNames = null)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			this.decider = decider;
			this.deciderCache = deciderCache;
			this.deciderReport = deciderReport;
			this.goalNames = goalNames;
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
				lastDeciderReason = null;
				decided = await NarrowToDecidedMethod(step, previousBuildError);
				classDescription = decided;
			}
			else
			{
				// The module handed its own description in, so it builds this step itself: sql, c#,
				// a regex. The llm is the right answer here and the decider was never in it.
				deciderReport?.Record(step.Goal!, step, PLang.Building.DeciderOutcome.ModuleBuildsItsOwn);
			}
			if (decided == null && classDescription == null)
			{
				deciderReport?.Record(step.Goal!, step,
					lastDeciderReason == null ? PLang.Building.DeciderOutcome.NotOffered : PLang.Building.DeciderOutcome.FellBack,
					lastDeciderReason);
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
				deciderReport?.Record(step.Goal!, step,
					built != null ? PLang.Building.DeciderOutcome.Decided : PLang.Building.DeciderOutcome.FellBack,
					built != null ? null : lastDeciderReason ?? "the decider gave no answer");

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
			if (decider == null || previousBuildError != null || step.Goal?.IsSystem == true || !PLang.Building.BuilderDecider.IsOn())
			{
				lastDeciderReason = decider == null ? "no decider" : previousBuildError != null ? "this is a retry, the llm gets the second attempt" : step.Goal?.IsSystem == true ? "system goal" : "decider is off";
				return null;
			}

			var programType = typeHelper.GetRuntimeType(module);
			if (programType == null)
			{
				lastDeciderReason = $"No runtime type for {module}, llm builds the step";
				return null;
			}

			var (classDescription, error) = new ClassDescriptionHelper().GetClassDescription(programType);
			if (error != null || classDescription == null)
			{
				// Silent until now, and it is the single biggest reason steps go to the llm in this
				// app: 57 of them, every one an `if ... then`, because the module's own description
				// could not be built and nobody said so.
				lastDeciderReason = $"Could not describe {module}, llm builds the step: {error?.Message?.ReplaceLineEndings(" ").Trim().MaxLength(140)}";
				return null;
			}
			// One method is not a decision, but it is an answer: the method is known without asking,
			// and its parameters can still be decided. Giving up here sent every `call goal X` to the
			// llm over a choice that had already been made.
			var onlyMethod = classDescription.Methods.Select(m => m.MethodName).Distinct().ToList();
			if (onlyMethod.Count == 1)
			{
				logger.LogInformation($"{step.LineNumber}: {module} has one method, {onlyMethod[0]}, so nothing to choose");
				return new ClassDescription
				{
					Description = classDescription.Description,
					ExampleInformation = classDescription.ExampleInformation,
					Methods = classDescription.Methods.Where(m => m.MethodName == onlyMethod[0]).ToList(),
					SupportingObjects = classDescription.SupportingObjects
				};
			}

			// Usually already answered for the whole goal in one request by StepBuilder's prefetch.
			var choice = deciderCache?.ForGoal(step.Goal!).Method(step);
			IError? deciderError = null;
			if (choice == null)
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
				FellBack(step, $"Decider method {choice?.Method} ({choice?.Confidence:0.00}) not trusted, llm picks from all methods. Contenders: {contenders}");
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
				// The examples an author wrote are the plainest statement of what a method is for, and
				// some methods have examples and no description at all: IsEmpty carries
				// `if %id% is empty then call Create` and nothing else, so without them it went to the
				// decider as a bare signature and lost to SimpleCondition, whose description happens to
				// list isEmpty among its operators.
				var examples = method.Examples == null || method.Examples.Count == 0
					? ""
					: " Examples: " + string.Join(" ", method.Examples);
				criteria[method.MethodName] = $"{method.MethodName}({parameters}){description}{examples}";
			}
			return criteria;
		}

		private const string NoneOption = "__none__";
		private const string EmptyListOption = "__empty_list__";
		private const string PassedThroughOption = "__passed__";

		private static bool IsList(string type)
		{
			return type.StartsWith("System.Collections.Generic.List`1[")
				|| type.StartsWith("System.Collections.Generic.IReadOnlyList`1[")
				|| type.StartsWith("System.Collections.Generic.IList`1[");
		}

		// With the method known, every parameter is a choice from a finite set the step itself
		// provides: a %variable% in scope, a quoted literal, a number, an enum name or a bool, plus
		// "none of these", which means "leave unset" for an optional parameter and "the value is
		// something else" for a required one. All questions go in one call. It is all or nothing:
		// one parameter the step cannot answer (a complex type, a string not present in the step,
		// which is what SQL and code look like, or a low-confidence pick) and the whole step goes to
		// the llm, which still only sees this one method. On success the function is built here
		// and the llm is never called for the step.
		// The reason the last step fell back, kept so the build can report it rather than leaving it
		// in a log line somewhere. Set wherever DecideParameters or NarrowToDecidedMethod gives up.
		private string? lastDeciderReason;

		private Instruction? FellBack(GoalStep step, string reason)
		{
			lastDeciderReason = reason;
			logger.LogInformation($"{step.LineNumber}: {reason}");
			return null;
		}

		private async Task<Instruction?> DecideParameters(GoalStep step, ClassDescription decided, LlmRequest question)
		{
			if (decider == null || !PLang.Building.BuilderDecider.IsOn()) return null;
			lastDeciderReason = null;
			if (decided.Methods.Count != 1)
			{
				return FellBack(step, $"Decider leaves parameters to llm, {decided.Methods.Count} overloads of {decided.Methods.FirstOrDefault()?.MethodName}");
			}

			var plan = BuildParameterPlan(step, decided, variableHelper, memoryStack, goalNames);
			if (plan.GiveUpReason != null) return FellBack(step, plan.GiveUpReason);

			var method = plan.Method;
			var variables = plan.Variables;
			var literals = plan.Literals;
			var numbers = plan.Numbers;
			var questions = plan.Questions;
			var recordFields = plan.RecordFields;
			var returnType = plan.ReturnType;
			bool hasReturn = plan.HasReturn;

			// Usually already answered for the whole goal by StepBuilder's prefetch, the return along
			// with the methods and the parameters after it. A miss means this step asks for itself.
			var cached = deciderCache?.ForGoal(step.Goal!);
			var prefetched = cached?.Parameters(step);
			var cachedReturn = cached?.Return(step);

			// The goal, not just the step. The questions are still about this one step, but the steps
			// around it are what make the engine decisive about a flag the step says nothing of:
			// asked with only its own text, `loadVariables` came back unsure at 0.18.
			var state = GoalStateFor(step);
			ParameterChoice? returnChoice = null;

			if (hasReturn)
			{
				returnChoice = cachedReturn;
				if (returnChoice == null && prefetched != null) prefetched.TryGetValue("__return__", out returnChoice);

				var returnError = returnChoice != null ? null : await AskReturn(step, method, plan, state, r => returnChoice = r);
				if (returnChoice == null)
				{
					return FellBack(step, $"Decider could not choose the return variable, llm fills parameters: {returnError?.Message}");
				}
				// The batched question is asked beside the method, before the method is known, so it
				// still offers "nowhere" even for a method that must return. Catch that here: an
				// answer that cannot be legal is not an answer.
				if (method.ReturnRequired && returnChoice.Choice == NoneOption)
				{
					return FellBack(step, $"Decider left {method.MethodName} without a return variable, which it requires, llm fills parameters");
				}

				state += returnChoice.Choice == NoneOption
					? "\n\nThis step does not write its result into any variable."
					: $"\n\nThis step writes its result into the variable {returnChoice.Choice}.";

				if (returnChoice.Choice != NoneOption) MarkReturnVariable(questions, returnChoice.Choice);
			}

			if (questions.Count == 0)
			{
				// Nothing to fill, but the return variable was still decided above and must be kept.
				var onlyReturn = new List<ReturnValue>();
				if (returnChoice != null && returnChoice.Choice != NoneOption)
				{
					if (returnChoice.Confidence < PLang.Building.BuilderDecider.ConfidenceThreshold)
					{
						return FellBack(step, $"Decider return target {returnChoice.Choice} ({returnChoice.Confidence:0.00}) not trusted, llm fills parameters");
					}
					onlyReturn.Add(new ReturnValue(returnType!, returnChoice.Choice));
				}
				if (!EveryValuePlaced(step, variables, literals, numbers, new(), onlyReturn, plan.JsonSpans)) return null;
				return BuildDecided(step, method, new(), onlyReturn, question);
			}

			Dictionary<string, ParameterChoice>? choices = prefetched;
			IError? error = null;
			if (choices == null)
			{
				(choices, error) = await decider.ChooseParameters(state, method.MethodName, questions);
			}
			if (error != null || choices == null)
			{
				return FellBack(step, $"Decider could not choose parameters, llm fills them: {error?.Message}");
			}
			if (returnChoice != null) choices["__return__"] = returnChoice;

			choices = DropDuplicateClaims(step, choices);

			var parameters = new List<Parameter>();
			foreach (var parameter in method.Parameters ?? new())
			{
				if (plan.Dictionaries.TryGetValue(parameter.Name, out var keys))
				{
					var entries = new Newtonsoft.Json.Linq.JObject();
					foreach (var key in keys)
					{
						if (!choices.TryGetValue($"{parameter.Name}#{key}", out var entry)) continue;
						// Every answer has to be sure, the nones too: an unsure none silently drops a value
						// the step asked for, and the step would build and do less than it says.
						if (entry.Confidence < PLang.Building.BuilderDecider.ConfidenceThreshold)
						{
							return FellBack(step, $"Decider unsure what the step does with {key} ({entry.Confidence:0.00}), llm fills parameters");
						}
						if (entry.Choice == NoneOption) continue;
						if (entry.Choice == PassedThroughOption)
						{
							// `return %result%` is {"result": "%result%"}, keyed by the name without its %.
							entries[key.Trim('%')] = key;
							continue;
						}
						entries[key] = entry.Choice;
					}
					if (entries.Count == 0)
					{
						return FellBack(step, $"Decider found nothing set by the step for {parameter.Name}, llm fills parameters");
					}
					logger.LogInformation($"{step.LineNumber}: Decider set {parameter.Name} = {entries.ToString(Newtonsoft.Json.Formatting.None)}");
					parameters.Add(new Parameter(parameter.Type, parameter.Name, entries));
					continue;
				}

				if (recordFields.TryGetValue(parameter.Name, out var fields))
				{
					var record = new Dictionary<string, object?>();
					foreach (var field in fields)
					{
						var fieldKey = $"{parameter.Name}.{field.Name}";
						// Nothing in the step could fill it, so it was never asked and keeps its default.
						if (!questions.ContainsKey(fieldKey)) continue;
						if (!choices.TryGetValue(fieldKey, out var fieldChoice)) return FellBack(step, $"Decider got no answer for {fieldKey}, llm fills parameters");

						// A word that names no goal in the app is not a goal name, whatever the engine's
						// confidence in it, so that is settled before confidence is looked at. An `if x is
						// empty then` with a block under it calls no goal at all, and the engine was
						// picking the word "if" out of the step and the step was going to the llm over it.
						if (field.ValueSource == "goal" && !fieldChoice.Choice.StartsWith("%") && fieldChoice.Choice != NoneOption
							&& goalNames != null && goalNames.Count > 0
							&& !goalNames.Any(g => g.Equals(fieldChoice.Choice, StringComparison.OrdinalIgnoreCase)
								|| fieldChoice.Choice.EndsWith("/" + g, StringComparison.OrdinalIgnoreCase)))
						{
							if (parameter.IsRequired) return FellBack(step, $"Decider chose {fieldChoice.Choice} for {fieldKey}, which is no goal in this app, llm fills parameters");

							logger.LogDebug($"{step.LineNumber}: {fieldChoice.Choice} is no goal in this app, so {parameter.Name} is left unset");
							record.Clear();
							break;
						}
						if (!field.IsRequired && MeansLeaveAlone(field, fieldChoice.Choice))
						{
							var sure = ConfidenceInLeavingUnset(field, fieldChoice);
							if (sure < PLang.Building.BuilderDecider.ConfidenceThreshold)
							{
								return FellBack(step, $"Decider is unsure whether to leave {fieldKey} unset ({sure:0.00}), llm fills parameters");
							}
							logger.LogDebug($"{step.LineNumber}: Decider leaves {fieldKey} unset ({sure:0.00})");
							continue;
						}
						if (fieldChoice.Confidence < PLang.Building.BuilderDecider.ConfidenceThreshold)
						{
							return FellBack(step, $"Decider field {fieldKey}={fieldChoice.Choice} ({fieldChoice.Confidence:0.00}, {(field.IsRequired ? "required" : "optional")}) not trusted, llm fills parameters");
						}
						if (fieldChoice.Choice == NoneOption)
						{
							// A required field of an optional record with nothing to put in it does not
							// fail the step, it means the record itself is not set. `run agent, messages:
							// %messages%, tools: %tools%` says nothing of onToolCall, and its required
							// name was being demanded of a record the step never asked for.
							if (!parameter.IsRequired)
							{
								logger.LogDebug($"{step.LineNumber}: Decider leaves {parameter.Name} unset, the step gives no {field.Name}");
								record.Clear();
								break;
							}
							return FellBack(step, $"Decider found no value in the step for required field {fieldKey}, llm fills parameters");
						}
						var fieldValue = ToValue(field, fieldChoice.Choice);
						if (fieldValue == null)
						{
							return FellBack(step, $"Decider could not type {fieldKey}={fieldChoice.Choice} as {field.Type}, llm fills parameters");
						}
						logger.LogInformation($"{step.LineNumber}: Decider set {fieldKey} = {fieldChoice.Choice} ({fieldChoice.Confidence:0.00})");
						record[field.Name] = fieldValue;
					}
					// Cleared above: the record is optional and the step gives it nothing, so it is left
					// out entirely rather than built empty.
					if (record.Count == 0 && !parameter.IsRequired) continue;

					var recordValue = RecordValue(parameter, record);
					if (recordValue == null)
					{
						return FellBack(step, $"Decider could not construct {parameter.Name} ({parameter.Type}), llm fills parameters");
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
				if (!choices.TryGetValue(parameter.Name, out var choice)) return FellBack(step, $"Decider got no answer for {parameter.Name}, llm fills parameters");
				if (!parameter.IsRequired && MeansLeaveAlone(parameter, choice.Choice))
				{
					var sure = ConfidenceInLeavingUnset(parameter, choice);
					if (sure < PLang.Building.BuilderDecider.ConfidenceThreshold)
					{
						return FellBack(step, $"Decider is unsure whether to leave {parameter.Name} unset ({sure:0.00}), llm fills parameters");
					}
					continue;
				}
				if (choice.Confidence < PLang.Building.BuilderDecider.ConfidenceThreshold)
				{
					return FellBack(step, $"Decider parameter {parameter.Name}={choice.Choice} ({choice.Confidence:0.00}) not trusted, llm fills parameters");
				}
				if (choice.Choice == NoneOption)
				{
					return FellBack(step, $"Decider found no value in the step for required parameter {parameter.Name}, llm fills parameters");
				}
				var value = ToValue(parameter, choice.Choice);
				if (value == null)
				{
					return FellBack(step, $"Decider could not type {parameter.Name}={choice.Choice} as {parameter.Type}, llm fills parameters");
				}
				logger.LogInformation($"{step.LineNumber}: Decider set {parameter.Name} = {choice.Choice} ({choice.Confidence:0.00})");
				parameters.Add(new Parameter(parameter.Type, parameter.Name, value));
			}

			var returnValues = new List<ReturnValue>();
			if (hasReturn && choices.TryGetValue("__return__", out var target))
			{
				if (target.Confidence < PLang.Building.BuilderDecider.ConfidenceThreshold)
				{
					return FellBack(step, $"Decider return target {target.Choice} ({target.Confidence:0.00}) not trusted, llm fills parameters");
				}
				if (target.Choice != NoneOption)
				{
					logger.LogInformation($"{step.LineNumber}: Decider set return to {target.Choice} ({target.Confidence:0.00})");
					returnValues.Add(new ReturnValue(returnType!, target.Choice));
				}
			}

			if (!EveryValuePlaced(step, variables, literals, numbers, parameters, returnValues, plan.JsonSpans)) return null;
			return BuildDecided(step, method, parameters, returnValues, question);
		}

		// Every question is answered on its own, so one value in the step can be claimed by several
		// parameters at once: `render "x.html", write to %html%` had %html% as the return variable at
		// 0.95 and as the render target at 0.31. A value written once in a step belongs to one
		// parameter, so the surest claim keeps it and the rest fall back to unset, which the loops
		// below then reject for a required parameter and accept for an optional one.
		// Choosing an optional parameter's own default value and leaving it unset are the same
		// instruction, so both mean leave it alone.
		private static bool MeansLeaveAlone(IPropertyDescription parameter, string choice)
		{
			if (choice == NoneOption) return true;
			return parameter.DefaultValue != null
				&& string.Equals(choice, parameter.DefaultValue.ToString(), StringComparison.OrdinalIgnoreCase);
		}

		// How sure the engine is that a parameter should be left alone. Leaving it unset and choosing
		// its default value are the same instruction, so the probability of both counts: asked about
		// loadVariables, whose default is false, the engine answered `__none__` at 0.27 while also
		// giving `false` 0.5, and reading only the first made a settled answer look like doubt.
		private static double ConfidenceInLeavingUnset(IPropertyDescription parameter, ParameterChoice choice)
		{
			if (choice.Probabilities == null || parameter.DefaultValue == null) return choice.Confidence;

			var asDefault = parameter.DefaultValue.ToString();
			double total = 0;
			foreach (var probability in choice.Probabilities)
			{
				if (probability.Key == NoneOption || string.Equals(probability.Key, asDefault, StringComparison.OrdinalIgnoreCase))
				{
					total += probability.Value;
				}
			}
			return Math.Max(choice.Confidence, total);
		}

		// Once the return variable is known, every other question that offers it says so. It is not
		// removed: a step may well read and write the same variable, `calculate %number% + 1, write
		// to %number%`, so it stays a legal answer and the engine is told what it is.
		internal static void MarkReturnVariable(Dictionary<string, ParameterQuestion> questions, string? returnVariable)
		{
			if (string.IsNullOrEmpty(returnVariable) || returnVariable == NoneOption) return;

			foreach (var name in questions.Keys.ToList())
			{
				var question = questions[name];
				if (!question.Candidates.ContainsKey(returnVariable)) continue;

				var candidates = new Dictionary<string, string>(question.Candidates);
				candidates[returnVariable] = $"the variable {returnVariable}, which is where this step writes its result";
				questions[name] = question with { Candidates = candidates };
			}
		}

		// Only a value the step actually writes can belong to one parameter. true and false are not
		// values taken from the step, they are the whole option set of every bool, so two bools
		// answering false are not competing for anything and neither claim should be dropped.
		private static bool IsShareableConstant(string choice)
		{
			return choice.Equals("true", StringComparison.OrdinalIgnoreCase)
				|| choice.Equals("false", StringComparison.OrdinalIgnoreCase);
		}

		private Dictionary<string, ParameterChoice> DropDuplicateClaims(GoalStep step, Dictionary<string, ParameterChoice> choices)
		{
			// The return variable never competes. A step may read and write the same variable, and
			// `calculate %number% + 1, write to %number%` would otherwise have one of the two claims
			// thrown away.
			var winners = choices
				.Where(c => c.Key != "__return__" && c.Value.Choice != NoneOption && !IsShareableConstant(c.Value.Choice))
				.GroupBy(c => c.Value.Choice)
				.Where(g => g.Count() > 1)
				.Select(g => g.OrderByDescending(c => c.Value.Confidence).First().Key)
				.ToHashSet();
			if (winners.Count == 0) return choices;

			var deduped = new Dictionary<string, ParameterChoice>();
			foreach (var choice in choices)
			{
				bool contested = choice.Key != "__return__" && !IsShareableConstant(choice.Value.Choice)
					&& choices.Any(other => other.Key != "__return__" && other.Key != choice.Key && other.Value.Choice == choice.Value.Choice);
				if (choice.Value.Choice != NoneOption && contested && !winners.Contains(choice.Key))
				{
					logger.LogInformation($"{step.LineNumber}: Decider gives {choice.Value.Choice} to another parameter, {choice.Key} is left unset");
					// Certain, because this is our conclusion and not the engine's guess: the value went
					// to a surer claim, so nothing is left for this one. Keeping the engine's confidence
					// for the value it no longer holds made the unset look like doubt and threw the step
					// away, which is how `render "x.html", cssSelector: "#main"` lost LayoutTarget at 0.63.
					deduped[choice.Key] = choice.Value with { Choice = NoneOption, Confidence = 1 };
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
		private bool EveryValuePlaced(GoalStep step, List<string> variables, List<string> literals, List<string> numbers, List<Parameter> parameters, List<ReturnValue> returnValues, List<string>? jsonSpans = null)
		{
			var placed = new List<string>();
			foreach (var parameter in parameters)
			{
				if (parameter.Value is Newtonsoft.Json.Linq.JToken token)
				{
					// The whole json, so anything written inside it counts as placed, not only the
					// values: a record built field by field has its fields here too.
					placed.Add(token.ToString(Newtonsoft.Json.Formatting.None));
					if (token is Newtonsoft.Json.Linq.JObject record)
					{
						placed.AddRange(record.Properties().Select(p => p.Value.ToString()));
					}
				}
				else if (parameter.Value != null)
				{
					placed.Add(parameter.Value.ToString()!);
				}
			}
			placed.AddRange(returnValues.Select(r => r.VariableName));

			// A value written only inside a {...} or [...] span belongs to that span, not to the step:
			// the %ideaId% of `call back data: {"ideaId": "%ideaId%"}` is part of one value and has no
			// parameter of its own to land in.
			var unplaced = variables.Concat(literals).Concat(numbers)
				.Where(value => !OnlyInsideSpan(step.Text, value, jsonSpans))
				.Where(value => !placed.Any(p => p.Contains(value, StringComparison.OrdinalIgnoreCase)))
				.ToList();
			if (unplaced.Count == 0) return true;

			FellBack(step, $"Decider found no parameter for {string.Join(", ", unplaced)} in the step, llm builds it");
			return false;
		}

		// Every step of the goal, with the one being decided named, so the answers are given in
		// context. Falls back to the step alone when the goal is not loaded.
		private static string GoalStateFor(GoalStep step)
		{
			var steps = step.Goal?.GoalSteps;
			if (steps == null || steps.Count < 2) return step.Text;

			var text = new System.Text.StringBuilder($"This is a plang goal called {step.Goal!.GoalName}. Its steps are numbered.\n\n");
			foreach (var other in steps)
			{
				text.AppendLine($"step {other.Index + 1}: {other.Text.Trim()}");
			}
			text.AppendLine($"\nThe questions below are all about step {step.Index + 1}: {step.Text.Trim()}");
			return text.ToString();
		}

		private async Task<IError?> AskReturn(GoalStep step, MethodDescription method, ParameterPlan plan, string state, Action<ParameterChoice?> keep)
		{
			var (answer, error) = await decider!.ChooseParameters(state, method.MethodName, plan.ReturnQuestion);
			if (error != null || answer == null || !answer.TryGetValue("__return__", out var choice)) return error;
			keep(choice);
			return null;
		}

		// What the decider will be asked about one step, built once and used by both callers: the
		// per step path here, and StepBuilder's prefetch, which merges the plans of every step in a
		// goal into one request. Two callers building their own questions would drift.
		internal record ParameterPlan(
			MethodDescription Method,
			Dictionary<string, ParameterQuestion> Questions,
			Dictionary<string, ParameterQuestion> ReturnQuestion,
			Dictionary<string, List<PrimitiveDescription>> RecordFields,
			List<string> Variables, List<string> Literals, List<string> Numbers, List<string> JsonSpans,
			Dictionary<string, List<string>> Dictionaries,
			string? ReturnType, string? GiveUpReason)
		{
			public bool HasReturn => ReturnQuestion.Count > 0;
		}

		internal static ParameterPlan BuildParameterPlan(GoalStep step, ClassDescription decided, VariableHelper variableHelper, MemoryStack memoryStack, List<string>? goalNames = null)
		{
			var method = decided.Methods[0];
			// Paths come back without their %; the runtime only treats a value as a variable when it
			// is wrapped, so wrap here or a decided variable would be written as a literal string.
			var variables = variableHelper.GetVariables(step.Text, memoryStack).Select(v => "%" + v.Path.Trim('%') + "%").Distinct().ToList();
			var jsonSpans = JsonSpans(step.Text);
			var words = StepWords(step.Text);
			var literals = QuotedLiterals(step.Text, jsonSpans);
			var numbers = Numbers(step.Text);

			var questions = new Dictionary<string, ParameterQuestion>();
			var recordFields = new Dictionary<string, List<PrimitiveDescription>>();
			var dictionaries = new Dictionary<string, List<string>>();

			ParameterPlan GiveUp(string reason) => new(method, questions, new(), recordFields, variables, literals, numbers, jsonSpans, dictionaries, null, reason);

			foreach (var parameter in method.Parameters ?? new())
			{
				if (IsSimpleDictionary(parameter.Type ?? "") && variables.Count > 0)
				{
					var entries = DictionaryQuestions(parameter, step.Text, method.MethodName, variables, literals);
					if (entries.Count > 0)
					{
						foreach (var entry in entries) questions[$"{parameter.Name}#{entry.Key}"] = entry.Value;
						// The variables the questions are about, not the question keys: a step with one variable
						// and nothing else asks only whether it is passed through, and keying off the questions
						// then left nothing to read the answers against.
						dictionaries[parameter.Name] = variables.ToList();
						continue;
					}
				}

				var candidates = ParameterCandidates(parameter, variables, literals, numbers, jsonSpans, words);
				if (candidates == null)
				{
					// A record of simple fields (TextMessage is one) is decided field by field, the same
					// way a method is decided parameter by parameter: required fields are asked, fields
					// with a constructor default that the step never mentions keep that default.
					var fields = RecordFields(parameter);
					if (fields == null)
					{
						// Not a choice and not a record to take apart, but the step may still name a
						// variable holding the value, or write it inline. This used to be unreachable
						// for an optional parameter, which was skipped before it got here: the tools of
						// `run agent, messages: %messages%, tools: %tools%` were never asked about, and
						// the step then died because %tools% had landed nowhere.
						var complex = ComplexCandidates(parameter, variables, jsonSpans);
						if (complex == null)
						{
							if (!parameter.IsRequired) continue;
							return GiveUp($"Decider leaves parameters to llm, {parameter.Name} ({parameter.Type}) is not a choice");
						}

						questions[parameter.Name] = new ParameterQuestion(Describe(parameter, null), complex);
						continue;
					}

					var fieldQuestions = new Dictionary<string, ParameterQuestion>();
					var recordIsUnanswerable = false;
					foreach (var field in fields)
					{
						var fieldCandidates = ParameterCandidates(field, variables, literals, numbers, jsonSpans, words);
						if (fieldCandidates == null)
						{
							// Nothing in the step can fill this field. If the record itself is optional then
							// the record is not set at all, which is what the llm writes for it:
							// `add route /admin, call /admin/Overview` sets no requestProperties, and its
							// required Methods was being demanded of a record the step never asked for.
							if (!parameter.IsRequired) { recordIsUnanswerable = true; break; }
							return GiveUp($"Decider leaves parameters to llm, {parameter.Name}.{field.Name} ({field.Type}) is not a choice");
						}
						if (fieldCandidates.Count == 0) continue;
						fieldQuestions[$"{parameter.Name}.{field.Name}"] = new ParameterQuestion(Describe(field, $"a field of the {parameter.Name} parameter"), fieldCandidates);
					}
					if (recordIsUnanswerable) continue;

					foreach (var fieldQuestion in fieldQuestions) questions[fieldQuestion.Key] = fieldQuestion.Value;
					recordFields[parameter.Name] = fields;
					continue;
				}
				if (candidates.Count == 0) continue;
				questions[parameter.Name] = new ParameterQuestion(Describe(parameter, null), candidates);
			}

			// Whether the step captures its result is settled on its own, before the rest, because
			// other parameters depend on it and questions in one request cannot see each other's
			// answers: RenderTemplate's RenderToOutputstream has to be true exactly when nothing is
			// captured, and asked blind it stayed unset, which renders the page and sends nothing.
			// No variable anywhere in the step means there is nothing it could write its result into,
			// so there is nothing to choose and the question is not worth a request. This is not
			// reading the step: a plang variable is %name% whatever language the step is written in.
			var returnType = method.ReturnValue?.Type;
			var returnQuestion = new Dictionary<string, ParameterQuestion>();
			if (variables.Count > 0 && !string.IsNullOrEmpty(returnType) && !returnType.EndsWith("Void", StringComparison.OrdinalIgnoreCase))
			{
				var targets = variables.ToDictionary(v => v, v => $"write the result into the variable {v}");

				// A method declared [ReturnRequired] always writes its result somewhere, so "nowhere"
				// is not one of the answers. AddToList is one, and offering it anyway left the engine
				// splitting between the right variable and an answer that was never legal.
				var description = "The variable this step writes its result into.";
				if (!method.ReturnRequired)
				{
					targets[NoneOption] = "the result is not written to a variable";
					description += " Choose none when the step does not keep the result in a variable.";
				}
				returnQuestion["__return__"] = new ParameterQuestion(description, targets);
			}

			return new ParameterPlan(method, questions, returnQuestion, recordFields, variables, literals, numbers, jsonSpans, dictionaries, returnType, null);
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
		private static Dictionary<string, string>? ParameterCandidates(IPropertyDescription parameter, List<string> variables, List<string> literals, List<string> numbers, List<string> jsonSpans, List<string> words)
		{
			var type = parameter.Type ?? "";
			var candidates = new Dictionary<string, string>();

			// A goal name is written bare in a step, `call goal Advise`, so it is neither a quoted text
			// nor a variable and nothing was on offer. The options are the words the step itself
			// writes, not the goals the app has: a project's goal list is unbounded and would outgrow
			// the 255 options a question may carry, while a step stays a step. The app's goals are
			// still used, to check the answer afterwards rather than to supply it.
			if (parameter is PrimitiveDescription primitive && primitive.ValueSource == "goal")
			{
				foreach (var word in words) candidates[word] = $"the goal named {word}";
				foreach (var variable in variables) candidates[variable] = $"the goal named by the variable {variable}";
				if (candidates.Count == 0) return null;
				if (!parameter.IsRequired) candidates[NoneOption] = "none of these, leave the parameter unset so its default applies";
				return candidates;
			}

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

				// A required text the step writes without quotes has nothing to offer otherwise, and a
				// step is full of them: `add route /admin, call /admin/Overview` puts the route in bare.
				// The words of the step are added only when quotes and variables gave nothing, so a step
				// that does quote its values is not offered every other word in it as well.
				if (parameter.IsRequired && candidates.Count == 0)
				{
					foreach (var word in words) candidates[word] = $"the word {word} written in the step";
				}
				if (type == "System.Object")
				{
					foreach (var span in jsonSpans) candidates[span] = $"the value {span} written in the step";
				}
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
			// A bool keeps both of its answers. Removing the one that matches the default leaves
			// `true` against `leave it unset`, and that indirection is what a bool is worst at:
			// loadVariables answered 0.17 to 0.57 that way and answers 0.97 asked as true against
			// false. A chosen value that equals the default is simply not written, which is the same
			// instruction as leaving it out.
			bool isBool = type == "System.Boolean" || type == "System.Nullable`1[System.Boolean]";

			// Every other optional parameter is never offered its own default as an option. Choosing
			// it and leaving the parameter alone are the same instruction, so offering both split the
			// probability between two answers that do the same thing.
			if (!isBool && !parameter.IsRequired && parameter.DefaultValue != null)
			{
				var asDefault = parameter.DefaultValue.ToString();
				var sameAsDefault = candidates.Keys.FirstOrDefault(c => string.Equals(c, asDefault, StringComparison.OrdinalIgnoreCase));
				if (sameAsDefault != null) candidates.Remove(sameAsDefault);
			}

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
		private static List<PrimitiveDescription>? RecordFields(IPropertyDescription parameter)
		{
			var fields = new List<PrimitiveDescription>();
			return CollectRecordFields(parameter.Type ?? "", "", fields, 0) ? fields : null;
		}

		// Walks a record's constructor and collects the leaves that can be decided, naming each by
		// its dotted path. A record inside a record is walked too: `render` takes a
		// RenderTemplateOptions holding a RenderMessage, so the questions are
		// options.RenderMessage.Content and options.RenderMessage.Target. Depth is capped because a
		// type graph can be deep or cyclic, and a leaf that is neither a choice nor a record it can
		// walk gives up on the whole step.
		private static bool CollectRecordFields(string typeName, string prefix, List<PrimitiveDescription> fields, int depth)
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
					ValueSource = type == typeof(PLang.Models.GoalToCallInfo) && field.Name == "name" ? "goal" : null,
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

				if (LeaveUnset(description)) continue;
				if (IsChoiceType(description.Type))
				{
					fields.Add(description);
					continue;
				}
				if (!CollectRecordFields(description.Type, path, fields, depth + 1)) return false;
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
			if (instance == null) return null;

			// A record can carry properties that are derived rather than given, such as
			// RenderTemplateOptions.GuessIfTemplateFile, which reads whether Content looks like a file
			// name. They are computed at runtime and the llm never writes them, so writing them into
			// the instruction would put something in the .pr that is not an input.
			var json = Newtonsoft.Json.Linq.JObject.FromObject(instance);
			foreach (var property in instance.GetType().GetProperties())
			{
				bool derived = !property.CanWrite
					|| System.Reflection.CustomAttributeExtensions.GetCustomAttributes(property, typeof(PLang.Attributes.LlmIgnoreAttribute)).Any();
				if (derived) json.Remove(property.Name);
			}
			return json;
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
		// object or a list of objects. There is nothing to offer for it, and EveryValuePlaced
		// refuses the step when a value written in it found no home.
		//
		// This used to also look for the parameter's name in the step text. That is reading plang
		// code, and plang has no syntax: a step is intent, written in whatever language the
		// developer thinks in, so an English parameter name matches nothing in `- skrifa í %nafn%`
		// and every optional parameter would quietly take its default.
		private static bool LeaveUnset(IPropertyDescription parameter)
		{
			return !parameter.IsRequired && !IsChoiceType(parameter.Type ?? "");
		}

		private static object? ToValue(IPropertyDescription parameter, string choice)
		{
			var listType = parameter.Type ?? "";
			if (choice == EmptyListOption) return new Newtonsoft.Json.Linq.JArray();
			if (IsStringList(listType)) return new List<string> { choice };

			if (choice.StartsWith("%") && choice.EndsWith("%")) return choice;

			// A value written inline goes in as the object it is, not as the text of it, because that
			// is what the llm writes and what the runtime reads. Newtonsoft is lenient enough for the
			// approximate json a step may hold, `[{name:john}]`, but when it cannot read the span the
			// decider declines rather than writing something the runtime would choke on.
			if ((choice.StartsWith("{") && choice.EndsWith("}")) || (choice.StartsWith("[") && choice.EndsWith("]")))
			{
				try
				{
					// A step writes a variable bare inside the value, `{"messages": %messages%}`, which
					// is not json a parser will take. Quoting it is what the llm writes for the same
					// step, and it is a token change, not a reading of the step: %name% is a plang
					// variable whatever language the step is written in.
					var quoted = System.Text.RegularExpressions.Regex.Replace(choice,
						"(?<![\"'\\w])(%[^%\"'\\s]+%)(?![\"'\\w])", "\"$1\"");
					return Newtonsoft.Json.Linq.JToken.Parse(quoted);
				}
				catch (Exception)
				{
					return null;
				}
			}

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

		// The quoted texts a step writes, leaving out anything inside a {...} or [...] span. The keys
		// and values of `add {"who": "advisor", "text": "halló"} to list` are part of that one value,
		// not four values of their own: offered separately they are noise, and the rule that every
		// value must find a parameter then failed the step over the word "text".
		private static List<string> QuotedLiterals(string text, List<string>? jsonSpans = null)
		{
			var literals = new List<string>();
			foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text ?? "", "\"([^\"]*)\"|'([^']*)'"))
			{
				if (InsideSpan(text ?? "", match.Index, jsonSpans)) continue;

				var literal = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
				if (!literals.Contains(literal)) literals.Add(literal);
			}
			return literals;
		}

		// Every occurrence of the value in the step text sits inside a json span.
		private static bool OnlyInsideSpan(string text, string value, List<string>? spans)
		{
			if (spans == null || spans.Count == 0 || string.IsNullOrEmpty(value)) return false;

			bool found = false;
			int at = text.IndexOf(value, StringComparison.OrdinalIgnoreCase);
			while (at >= 0)
			{
				found = true;
				if (!InsideSpan(text, at, spans)) return false;
				at = text.IndexOf(value, at + 1, StringComparison.OrdinalIgnoreCase);
			}
			return found;
		}

		private static bool InsideSpan(string text, int index, List<string>? spans)
		{
			if (spans == null) return false;

			foreach (var span in spans)
			{
				int at = text.IndexOf(span, StringComparison.Ordinal);
				while (at >= 0)
				{
					if (index > at && index < at + span.Length) return true;
					at = text.IndexOf(span, at + 1, StringComparison.Ordinal);
				}
			}
			return false;
		}

		// The balanced {...} and [...] spans written in a step, taken whole and unexamined. A step may
		// hold something that is only approximately json, `set %list% = [{name:john}]`, so nothing
		// here validates or interprets it: it is a token, like a quoted string or a number, and it
		// looks the same whatever language the step is written in.
		// The bare words a step writes, as tokens. Not a reading of the step: a word is a word in any
		// language, and which of them means something is the decider's job, not this method's.
		private static List<string> StepWords(string text)
		{
			var words = new List<string>();
			foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text ?? "", @"[\p{L}_/][\p{L}\p{N}_/.-]*"))
			{
				var word = match.Value.Trim('.', '-', '/');
				if (word.Length < 2 || words.Contains(word)) continue;
				words.Add(match.Value);
			}
			return words;
		}

		private static List<string> JsonSpans(string text)
		{
			var spans = new List<string>();
			if (string.IsNullOrEmpty(text)) return spans;

			for (int i = 0; i < text.Length; i++)
			{
				if (text[i] != '{' && text[i] != '[') continue;

				var close = text[i] == '{' ? '}' : ']';
				int depth = 0;
				for (int j = i; j < text.Length; j++)
				{
					if (text[j] == text[i]) depth++;
					else if (text[j] == close) depth--;
					if (depth != 0) continue;

					var span = text.Substring(i, j - i + 1);
					if (span.Length > 2 && !spans.Contains(span)) spans.Add(span);
					i = j;
					break;
				}
			}
			return spans;
		}

		// A dictionary of values taken from the step, e.g. the keyValues of
		// `set %messages% = %stored.messages%, %chat% = %stored.log%`. It cannot be chosen as one
		// option because it is built rather than picked, but it decomposes into choices: ask, for
		// every variable the step writes, what that variable is set to. The answers that are not
		// none are the entries. No counting and no reading of the step, and one request still.
		private static bool IsSimpleDictionary(string type)
		{
			return type.StartsWith("System.Collections.Generic.Dictionary`2[")
				&& type.Contains("System.String")
				&& (type.Contains("System.Object") || type.Contains("System.String"));
		}

		private static Dictionary<string, ParameterQuestion> DictionaryQuestions(IPropertyDescription parameter, string stepText,
			string method, List<string> variables, List<string> literals)
		{
			var questions = new Dictionary<string, ParameterQuestion>();
			var tokens = variables.Concat(literals).ToList();

			foreach (var key in variables)
			{
				// One question for each value the step writes, with every shape such a dictionary takes
				// among the options: assigned something, handed over as it stands, or not part of it at
				// all. Asked as two questions, an assignment and a yes/no, the yes/no sat between 0.47
				// and 0.78 because it was a second question about a variable already asked about. One
				// choice between shapes is the question that was actually being asked.
				var candidates = new Dictionary<string, string>();
				foreach (var other in tokens)
				{
					if (other == key) continue;
					candidates[other] = other.StartsWith("%") ? $"the step sets {key} to the variable {other}" : $"the step sets {key} to the text \"{other}\"";
				}
				candidates[PassedThroughOption] = $"the step passes {key} itself to '{parameter.Name}', with its own value";
				candidates[NoneOption] = $"{key} is not part of '{parameter.Name}': it is a value being read, or not involved";

				questions[key] = new ParameterQuestion(
					$"The parameter '{parameter.Name}' of {method} holds one or more values."
						+ (string.IsNullOrWhiteSpace(parameter.Description) ? "" : " " + parameter.Description.Trim())
						+ $" What does this step do with {key}?",
					candidates, Standalone: true);
			}
			return questions;
		}

		// What can be offered for a parameter whose type is not a choice and which is not a record
		// this builder can take apart: a list of objects, a dictionary. The step cannot spell such a
		// value out option by option, but it can name a variable holding one, or write it inline, and
		// both of those are a choice. Without this, `run agent, tools: %tools%` was abandoned over a
		// parameter whose whole value is the word %tools%.
		private static Dictionary<string, string>? ComplexCandidates(IPropertyDescription parameter, List<string> variables, List<string> jsonSpans)
		{
			var candidates = new Dictionary<string, string>();
			foreach (var variable in variables) candidates[variable] = $"the variable {variable}";
			foreach (var span in jsonSpans) candidates[span] = $"the value {span} written in the step";

			// A list has one more legal value than the step can write: none at all. `add route /admin,
			// call /admin/Overview` names no path parameters, and its pathParameters is a required
			// list, so with nothing on offer the step went to the llm over a value that is simply empty.
			if (IsList(parameter.Type ?? ""))
			{
				candidates[EmptyListOption] = "the step names none of these, so the list is empty";
			}

			if (candidates.Count == 0) return null;

			candidates[NoneOption] = parameter.IsRequired
				? "none of these, the value is something else"
				: "none of these, leave the parameter unset so its default applies";
			return candidates;
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
