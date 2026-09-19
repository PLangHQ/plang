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

			// The goal's parameters are usually already built, in one request for the whole goal,
			// before any step gets here. A miss is not a failure: the step asks for itself, which is
			// what happened before this existed. A retry is never served from it either, or a step
			// whose answer failed validation would be handed the same answer again.
			if (previousBuildError == null && responseType == typeof(GenericFunction) && step.Goal != null)
			{
				// The answer is left in the cache after it is read. Some steps come through here
				// twice, a conditional does, and dropping the answer on the first read sent the
				// second pass to the llm: the batch was filling all ten steps of a goal and four of
				// them were still being asked for again. A retry after an error never reaches this
				// at all, which is what previousBuildError guards.
				var prebuilt = deciderCache?.ForGoal(step.Goal).Function(step);
				if (prebuilt != null)
				{
					// Built by the llm, in one request for the goal, so it is not a decider win.
					// Recording it as one made the build report say a goal was built without the llm
					// when every parameter in it had come from an llm request.
					deciderReport?.Record(step.Goal, step, PLang.Building.DeciderOutcome.Batched);

					appendedSystemCommand.Clear();
					appendedAssistantCommand.Clear();
					assistant = "";
					system = "";
					return (InstructionCreator.Create(prebuilt.Value.Function, step, prebuilt.Value.Request), null);
				}
			}

			var question = GetLlmRequest(step, responseType, previousBuildError, classDescription);

			try
			{


				var llmStarted = Stopwatch.StartNew();
				(var result, var queryError) = await llmServiceFactory.CreateHandler().Query(question, responseType);
				deciderReport?.RecordLlmCall(step.Goal!, llmStarted.Elapsed);
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
				var methodStarted = Stopwatch.StartNew();
				(choice, deciderError) = await decider.ChooseMethod(step.Text, module, MethodCriteria(classDescription));
				deciderReport?.RecordDeciderCall(step.Goal!, methodStarted.Elapsed);
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


		// The decider answers which module and which method. It used to answer the parameters too,
		// and that is gone: about fifteen hundred lines that read a step's text to work out what each
		// parameter could be, because a chooser needs a finite list of options and a parameter has
		// none. `set the variable named %ble% to the number ten` needs the value 10 written, not
		// chosen. Parameters are the llm's job now, and a goal's are built in one request by
		// BatchedInstructionBuilder.

		private string? lastDeciderReason;

		private Instruction? FellBack(GoalStep step, string reason)
		{
			lastDeciderReason = reason;
			logger.LogInformation($"{step.LineNumber}: {reason}");
			return null;
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
		public string GetDefaultSystemText(GoalStep step) => DefaultSystemText();

		// The same text, reachable without a builder instance. The batched builder sends it with two
		// corrections, and the corrections have to be made against this exact text, not a copy of it
		// that drifts: every rule added here has to reach both paths.
		public static string DefaultSystemText()
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
A quoted text is COPIED out of the step, character for character, only the wrapping quotes are dropped. Do not retype it from memory and do not write your own version of it: read the characters in the step and reproduce them exactly. Never reword, never translate, never change a capital letter to a small one, never drop a prefix such as ""Error: "", and never drop or alter a letter. This matters most for text that is not English, e.g. Icelandic `raunveruleg` is not `raunverleg` and `Prófið` is not `prófið`: a single changed letter is a wrong answer. It is the text the app shows, not a description of it
Error handling is process by another step, if you see 'on error...' you can ignore it
If there is some api key, settings, config replace it with %Settings.NameOfApiKey% 
- NameOfApiKey should named in relation to what is happening if change is needed
Dictionary<T1, T2> is written as {{""name of the entry"": its value, ... }}. A dictionary parameter given a %variable% and no name takes the variable's own name, without the percent signs, as the name of the entry, e.g. %userId% => {{""userId"": ""%userId%""}}, and NOT {{""key"": ""userId"", ""value"": ""%userId%""}}, which is a dictionary of two entries called key and value
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
- The variable the step writes its result into goes here and nowhere else. It is never also the value of a parameter, not even one whose name mentions a variable or an output. A step that names a variable to write into and leaves ReturnValues null has thrown the result away.
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
